using DotSetupForge.Core.Models;

namespace DotSetupForge.Core.Analysis;

/// <summary>应用分析聚合结果。</summary>
public sealed record ApplicationAnalysisResult
{
    public bool Success { get; init; }

    /// <summary>主程序完整路径。</summary>
    public string? MainExecutable { get; init; }

    public string ApplicationName { get; init; } = string.Empty;

    public ApplicationType ApplicationType { get; init; } = ApplicationType.Unknown;

    public string TargetFramework { get; init; } = string.Empty;

    /// <summary>共享框架名，如 Microsoft.WindowsDesktop.App。</summary>
    public string FrameworkName { get; init; } = string.Empty;

    public string FrameworkVersion { get; init; } = string.Empty;

    /// <summary>运行时名称，如 .NET Desktop Runtime。</summary>
    public string RuntimeName { get; init; } = string.Empty;

    public TargetArchitecture Architecture { get; init; } = TargetArchitecture.AnyCpu;

    public DeploymentMode DeploymentMode { get; init; } = DeploymentMode.Unknown;

    public string Version { get; init; } = string.Empty;

    public IReadOnlyList<ScannedFile> Files { get; init; } = [];

    public IReadOnlyList<DiagnosticMessage> Diagnostics { get; init; } = [];

    public static ApplicationAnalysisResult Failed(IReadOnlyList<DiagnosticMessage> diagnostics) =>
        new() { Success = false, Diagnostics = diagnostics };
}
