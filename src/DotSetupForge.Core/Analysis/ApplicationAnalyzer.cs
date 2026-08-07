using DotSetupForge.Core.Models;

namespace DotSetupForge.Core.Analysis;

/// <summary>应用分析聚合：目录 → ApplicationAnalysisResult。</summary>
public sealed class ApplicationAnalyzer
{
    private readonly DirectoryScanner _scanner = new();
    private readonly ExecutableResolver _resolver = new();
    private readonly RuntimeConfigParser _runtimeConfigParser = new();
    private readonly DepsJsonAnalyzer _depsJsonAnalyzer = new();
    private readonly PeArchitectureAnalyzer _peAnalyzer = new();
    private readonly AssemblyMetadataAnalyzer _assemblyAnalyzer = new();

    public ApplicationAnalysisResult Analyze(string directory, string? mainExecutable = null)
    {
        if (!Directory.Exists(directory))
        {
            return ApplicationAnalysisResult.Failed(
                [DiagnosticMessage.Error("DP1004", $"源目录不存在：{directory}")]);
        }

        var files = _scanner.Scan(directory);
        var diagnostics = new List<DiagnosticMessage>();

        // 1. 主程序
        var resolution = mainExecutable is not null
            ? ResolveFromUser(directory, files, mainExecutable, diagnostics)
            : _resolver.Resolve(files);

        if (resolution.Resolved is null)
        {
            if (resolution.Candidates.Count == 0)
            {
                return ApplicationAnalysisResult.Failed(
                    [DiagnosticMessage.Error("DP1001", "未发现主程序（无 *.exe）")]);
            }

            diagnostics.Add(DiagnosticMessage.Warning("DP1005",
                $"发现多个候选主程序，请指定 --main-exe：{string.Join(", ", resolution.Candidates)}"));
            return ApplicationAnalysisResult.Failed(diagnostics);
        }

        var mainExe = Path.Combine(directory, resolution.Resolved);
        var stem = Path.GetFileNameWithoutExtension(resolution.Resolved);

        // 2. runtimeconfig
        var runtimeConfigPath = Path.Combine(directory, $"{stem}.runtimeconfig.json");
        var runtimeConfig = _runtimeConfigParser.ParseFile(runtimeConfigPath);
        if (runtimeConfig is null)
        {
            diagnostics.Add(DiagnosticMessage.Error("DP1002", $"runtimeconfig.json 缺失：{runtimeConfigPath}"));
            return ApplicationAnalysisResult.Failed(diagnostics);
        }

        // 3. deps.json（可选，缺失仅警告）
        var depsPath = Path.Combine(directory, $"{stem}.deps.json");
        var deps = _depsJsonAnalyzer.ParseFile(depsPath);
        if (deps is null)
        {
            diagnostics.Add(DiagnosticMessage.Warning("DP1006", $"deps.json 缺失：{depsPath}"));
        }

        // 4. 架构：PE 优先
        var peArch = _peAnalyzer.Analyze(mainExe);
        var architecture = peArch.Architecture;
        var depsRids = deps?.Rids ?? [];

        if (peArch.Architecture == TargetArchitecture.AnyCpu)
        {
            var ridArch = GuessArchitectureFromRids(depsRids);
            if (ridArch is not null)
            {
                architecture = ridArch.Value;
                diagnostics.Add(DiagnosticMessage.Info(
                    $"主程序为 AnyCPU，从 deps.json RID 推断架构 {ridArch.Value}"));
            }
            else
            {
                diagnostics.Add(DiagnosticMessage.Warning("DP1101", "无法确定应用架构（PE 为 AnyCPU 且 deps.json 无 RID 信息）"));
            }
        }
        else if (depsRids.Count > 0)
        {
            var ridArch = GuessArchitectureFromRids(depsRids);
            if (ridArch is not null && ridArch != peArch.Architecture)
            {
                diagnostics.Add(DiagnosticMessage.Warning("DP1102",
                    $"架构冲突：PE={peArch.Architecture}(High)，deps.json RID={ridArch}(Medium)，以 PE 为准"));
            }
        }

        // 5. 元数据
        var metadata = _assemblyAnalyzer.Analyze(mainExe);

        // 6. Framework / Runtime 类型
        var framework = SelectPrimaryFramework(runtimeConfig, diagnostics);
        var runtimeName = framework.Name switch
        {
            "Microsoft.WindowsDesktop.App" => ".NET Desktop Runtime",
            "Microsoft.AspNetCore.App" => "ASP.NET Core Runtime",
            "Microsoft.NETCore.App" => ".NET Runtime",
            _ => string.Empty,
        };

        // 7. 程序类型
        var appType = DetectApplicationType(runtimeConfig, deps);

        // 8. 部署模式
        var deployment = DetectDeploymentMode(files, stem);

        var version = !string.IsNullOrEmpty(metadata.AssemblyVersion)
            ? metadata.AssemblyVersion
            : metadata.FileVersion;

        return new ApplicationAnalysisResult
        {
            Success = true,
            MainExecutable = mainExe,
            ApplicationName = stem, // 主程序文件名即产品名（apphost 命名约定）
            ApplicationType = appType,
            TargetFramework = runtimeConfig.Tfm,
            FrameworkName = framework.Name,
            FrameworkVersion = framework.Version,
            RuntimeName = runtimeName,
            Architecture = architecture,
            DeploymentMode = deployment,
            Version = version,
            Files = files,
            Diagnostics = diagnostics,
        };
    }

    private static ExecutableResolutionResult ResolveFromUser(
        string directory,
        IReadOnlyList<ScannedFile> files,
        string mainExecutable,
        List<DiagnosticMessage> diagnostics)
    {
        var full = Path.GetFullPath(Path.Combine(directory, mainExecutable));
        if (!File.Exists(full))
        {
            diagnostics.Add(DiagnosticMessage.Error("DP1001", $"指定的主程序不存在：{full}"));
            return new ExecutableResolutionResult(null, []);
        }

        return new ExecutableResolutionResult(
            Path.GetRelativePath(directory, full).Replace('\\', '/'),
            []);
    }

    private static FrameworkReference SelectPrimaryFramework(
        RuntimeConfig runtimeConfig,
        List<DiagnosticMessage> diagnostics)
    {
        // WindowsDesktop 优先（已含基础 Runtime），否则取 NETCore.App，最后取第一个
        var ordered = runtimeConfig.Frameworks
            .OrderByDescending(f => f.Name switch
            {
                "Microsoft.WindowsDesktop.App" => 3,
                "Microsoft.AspNetCore.App" => 2,
                "Microsoft.NETCore.App" => 1,
                _ => 0,
            })
            .ToList();

        if (ordered.Count == 0)
        {
            diagnostics.Add(DiagnosticMessage.Warning("DP1007", "runtimeconfig.json 未声明任何共享框架"));
            return new FrameworkReference(string.Empty, string.Empty);
        }

        return ordered[0];
    }

    private static ApplicationType DetectApplicationType(RuntimeConfig config, DepsJsonInfo? deps)
    {
        var hasDesktop = config.Frameworks.Any(f => f.Name == "Microsoft.WindowsDesktop.App");
        if (!hasDesktop)
        {
            return ApplicationType.Console;
        }

        if (deps is null || deps.ReferencedAssemblies.Count == 0)
        {
            return ApplicationType.Wpf;
        }

        // 通过 deps.json 引用程序集区分 WPF / WinForms
        var hasWpf = deps.ReferencedAssemblies.Contains("PresentationFramework.dll", StringComparer.OrdinalIgnoreCase)
            || deps.ReferencedAssemblies.Contains("WindowsBase.dll", StringComparer.OrdinalIgnoreCase);
        var hasWinForms = deps.ReferencedAssemblies.Contains("System.Windows.Forms.dll", StringComparer.OrdinalIgnoreCase);

        if (hasWpf)
        {
            return ApplicationType.Wpf;
        }

        return hasWinForms ? ApplicationType.WinForms : ApplicationType.Wpf;
    }

    private static DeploymentMode DetectDeploymentMode(IReadOnlyList<ScannedFile> files, string stem)
    {
        var rootFiles = files.Where(f => !f.RelativePath.Contains('/')).ToList();

        var hasRuntimeBinaries = rootFiles.Any(f =>
            f.Category == FileCategory.Runtime
            || f.FileName.Equals("hostfxr.dll", StringComparison.OrdinalIgnoreCase)
            || f.FileName.Equals("coreclr.dll", StringComparison.OrdinalIgnoreCase));

        if (hasRuntimeBinaries)
        {
            return DeploymentMode.SelfContained;
        }

        var hasManagedDll = files.Any(f =>
            f.FileName.Equals($"{stem}.dll", StringComparison.OrdinalIgnoreCase));

        return hasManagedDll ? DeploymentMode.FrameworkDependent : DeploymentMode.SingleFile;
    }

    private static TargetArchitecture? GuessArchitectureFromRids(IReadOnlyList<string> rids)
    {
        if (rids.Contains("win-x64", StringComparer.OrdinalIgnoreCase)) return TargetArchitecture.X64;
        if (rids.Contains("win-x86", StringComparer.OrdinalIgnoreCase)) return TargetArchitecture.X86;
        if (rids.Contains("win-arm64", StringComparer.OrdinalIgnoreCase)) return TargetArchitecture.Arm64;
        return null;
    }
}
