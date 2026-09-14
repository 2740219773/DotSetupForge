using DotSetupForge.Application.Analysis;
using DotSetupForge.Application.Packaging;
using DotSetupForge.Application.Runtime;
using DotSetupForge.Core.Analysis;
using DotSetupForge.Core.Models;
using DotSetupForge.Core.Packaging;
using DotSetupForge.Core.Runtime;
using DotSetupForge.Inno;

namespace DotSetupForge.Application.Build;

/// <summary>构建请求。</summary>
public sealed record BuildRequest(
    string SourceDirectory,
    PackageProject? Project = null,
    string? OutputDirectory = null);

/// <summary>构建工作流：分析 → Runtime 准备 → InstallerModel → installer.iss → ISCC 编译。</summary>
public sealed class BuildService
{
    private readonly ApplicationAnalysisService _analysis = new();
    private readonly InstallerModelBuilder _modelBuilder = new();
    private readonly RuntimeService _runtime = new();
    private readonly RuntimePrerequisiteBuilder _prerequisiteBuilder = new();
    private readonly InnoScriptGenerator _scriptGenerator = new();
    private readonly InnoSetupLocator _locator = new();
    private readonly InnoCompiler _compiler = new();

    public async Task<BuildResult> BuildAsync(
        BuildRequest request,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var started = DateTime.UtcNow;
        var diagnostics = new List<DiagnosticMessage>();

        // 1. 分析
        progress?.Report("分析应用...");
        var analysis = _analysis.Analyze(request.SourceDirectory, request.Project?.Product.MainExecutable);
        if (!analysis.Success)
        {
            return BuildResult.Fail(analysis.Diagnostics, DateTime.UtcNow - started);
        }

        // 2. Runtime 准备（智能离线：确保缓存）
        var runtimeDefinition = (RuntimeDefinition?)null;
        RuntimeDownloadResult? runtimeResult = null;
        PrerequisiteModel? legacyPrerequisite = null;
        if (ShouldEmbedRuntime(request.Project, analysis))
        {
            progress?.Report("准备 .NET Runtime...");
            var requirement = RuntimeRequirementFactory.FromAnalysis(analysis);
            runtimeResult = await _runtime.EnsureAsync(requirement, null, ct).ConfigureAwait(false);
            if (!runtimeResult.Success || runtimeResult.Definition is null)
            {
                return BuildResult.Fail(runtimeResult.Errors, DateTime.UtcNow - started);
            }

            runtimeDefinition = runtimeResult.Definition;
            progress?.Report(runtimeResult.FromCache
                ? $"Runtime 命中缓存：{runtimeResult.InstallerPath}"
                : $"Runtime 已下载：{runtimeResult.InstallerPath}");
        }

        if (ShouldEmbedLegacyFramework(request.Project, analysis))
        {
            progress?.Report("检查 .NET Framework 离线缓存...");
            var ensured = _runtime.EnsureLegacyFramework(analysis.FrameworkVersion);
            var cached = ensured.Cached;
            var package = cached is null ? null : _runtime.ResolveLegacyFramework(cached.Version);
            if (cached is null || package is null)
            {
                diagnostics.Add(DiagnosticMessage.Error("DP2011",
                    $"缺少 .NET Framework {analysis.FrameworkVersion} 离线安装器缓存。请在“运行环境”页导入与 ClickOnce Bootstrapper 定义匹配的完整 EXE。"));
                return BuildResult.Fail(diagnostics, DateTime.UtcNow - started);
            }

            legacyPrerequisite = _prerequisiteBuilder.BuildLegacyFramework(package, cached);
            progress?.Report($".NET Framework 命中缓存：{cached.InstallerPath}");
        }

        // 3. 构建安装模型
        progress?.Report("构建安装模型...");
        var model = _modelBuilder.Build(analysis, request.Project);

        if (runtimeDefinition is not null && runtimeResult is not null)
        {
            var requirement = RuntimeRequirementFactory.FromAnalysis(analysis);
            var prerequisite = _prerequisiteBuilder.Build(requirement, runtimeDefinition)
                with { SourcePath = runtimeResult.InstallerPath };
            model = model with
            {
                Prerequisites = model.Prerequisites.Append(prerequisite).ToList(),
            };
        }

        if (legacyPrerequisite is not null)
        {
            model = model with { Prerequisites = model.Prerequisites.Append(legacyPrerequisite).ToList() };
        }

        // 4. 生成 Inno 脚本
        var outputDirectory = request.OutputDirectory
            ?? (request.Project is null
                ? Path.Combine(Directory.GetCurrentDirectory(), "dist")
                : Path.GetFullPath(request.Project.Output.Directory));

        var baseFilename = $"{model.Product.Name}_Setup_{model.Product.Version}";
        var buildDirectory = Path.Combine(Path.GetTempPath(), "DotSetupForge", "build", $"iss-{Guid.NewGuid():N}");
        var branding = new WizardBrandAssetGenerator().Resolve(model, buildDirectory);
        foreach (var warning in branding.Warnings)
        {
            diagnostics.Add(DiagnosticMessage.Warning("DP3004", warning));
        }
        model = model with
        {
            WizardSmallImagePath = branding.SmallImagePath ?? string.Empty,
            WizardImagePath = branding.WelcomeImagePath ?? string.Empty,
        };
        progress?.Report("生成 installer.iss...");
        var scriptResult = _scriptGenerator.GenerateToFile(
            model,
            new InnoScriptOptions(baseFilename, outputDirectory),
            buildDirectory);

        // 4. 定位 ISCC
        var location = _locator.Locate();
        if (!location.Found)
        {
            diagnostics.Add(DiagnosticMessage.Error("DP3002", location.Error ?? "未找到 ISCC.exe"));
            return BuildResult.Fail(diagnostics, DateTime.UtcNow - started);
        }

        // 5. 编译
        progress?.Report("编译安装包...");
        var compile = await _compiler.CompileAsync(
            location.IsccPath!, scriptResult.IssPath, baseFilename, ct).ConfigureAwait(false);

        foreach (var entry in compile.Log.Where(e => e.Level == InnoLogLevel.Warning))
        {
            diagnostics.Add(DiagnosticMessage.Warning("DP3003", entry.Message));
        }

        if (!compile.Success)
        {
            foreach (var entry in compile.Log.Where(e => e.Level == InnoLogLevel.Error))
            {
                diagnostics.Add(DiagnosticMessage.Error("DP3001", entry.Message));
            }

            if (diagnostics.Count == 0)
            {
                diagnostics.Add(DiagnosticMessage.Error("DP3001", $"ISCC 退出码 {compile.ExitCode}"));
            }

            return BuildResult.Fail(diagnostics, DateTime.UtcNow - started);
        }

        // 6. 产物路径：{OutputDir}\{BaseFilename}.exe
        var artifact = compile.OutputFile
            ?? Path.Combine(outputDirectory, $"{baseFilename}.exe");

        // 7. 签名（可选，默认只签 Setup.exe）
        var signing = request.Project?.Signing;
        if (signing is { Enabled: true })
        {
            progress?.Report("数字签名...");
            var signed = await SignArtifactAsync(artifact, signing, ct).ConfigureAwait(false);
            if (!signed.Success)
            {
                return BuildResult.Fail([signed.Error], DateTime.UtcNow - started);
            }

            progress?.Report($"签名完成：{artifact}");
        }

        progress?.Report($"完成：{artifact}");
        return BuildResult.Ok(DateTime.UtcNow - started, artifact);
    }

    /// <summary>对产物执行 Authenticode 签名并验证。密码只从环境变量读取，绝不落盘。</summary>
    private static async Task<(bool Success, DiagnosticMessage Error)> SignArtifactAsync(
        string artifact,
        Core.Models.SigningOptions signing,
        CancellationToken ct)
    {
        var signer = new Core.Signing.SigningService();

        var location = signer.Locate();
        if (!location.Found || location.SigntoolPath is null)
        {
            return (false, DiagnosticMessage.Error("DP4001", location.Error ?? "未找到 signtool.exe"));
        }

        if (string.IsNullOrEmpty(signing.CertificatePath) || !File.Exists(signing.CertificatePath))
        {
            return (false, DiagnosticMessage.Error("DP4001", $"证书文件不存在：{signing.CertificatePath}"));
        }

        var password = Environment.GetEnvironmentVariable(
            Core.Signing.SigningService.PasswordEnvironmentVariable) ?? string.Empty;
        if (string.IsNullOrEmpty(password))
        {
            return (false, DiagnosticMessage.Error(
                "DP4001",
                $"未提供证书密码。请设置环境变量 {Core.Signing.SigningService.PasswordEnvironmentVariable}（密码不会写入 .pack.json）。"));
        }

        var signResult = await signer.SignAsync(
            artifact, signing.CertificatePath, password, signing.TimestampServer, ct).ConfigureAwait(false);
        if (!signResult.Success)
        {
            return (false, DiagnosticMessage.Error("DP4001", $"签名失败：{signResult.Error}"));
        }

        var verifyResult = await signer.VerifyAsync(artifact, ct).ConfigureAwait(false);
        if (!verifyResult.Success)
        {
            return (false, DiagnosticMessage.Error("DP4001", $"签名验证失败：{verifyResult.Error}"));
        }

        return (true, DiagnosticMessage.Error("DP4001", string.Empty));
    }

    /// <summary>是否内嵌运行时：Framework-dependent 且部署模式为智能离线/在线。</summary>
    private static bool ShouldEmbedRuntime(
        PackageProject? project,
        ApplicationAnalysisResult analysis)
    {
        if (analysis.DeploymentMode != DeploymentMode.FrameworkDependent)
        {
            return false;
        }

        var mode = project?.Runtime.Mode ?? RuntimeDeploymentMode.SmartOffline;
        return mode is RuntimeDeploymentMode.SmartOffline or RuntimeDeploymentMode.Online;
    }

    private static bool ShouldEmbedLegacyFramework(PackageProject? project, ApplicationAnalysisResult analysis) =>
        analysis.DeploymentMode == DeploymentMode.LegacyFramework &&
        (project?.Runtime.Mode ?? RuntimeDeploymentMode.SmartOffline) == RuntimeDeploymentMode.SmartOffline;
}
