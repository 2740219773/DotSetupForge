using DotSetupForge.Core.Models;

namespace DotSetupForge.Core.Runtime;

/// <summary>运行时安装包定义（来自官方 releases 元数据）。</summary>
public sealed record RuntimeDefinition(
    RuntimeFamily Family,
    string Version,
    TargetArchitecture Architecture,
    string DisplayName,
    string DownloadUrl,
    string FileName,
    string Sha256);

/// <summary>下载结果。</summary>
public sealed record RuntimeDownloadResult(
    bool Success,
    string? InstallerPath,
    bool FromCache,
    RuntimeDefinition? Definition,
    IReadOnlyList<DotSetupForge.Core.Models.DiagnosticMessage> Errors)
{
    public static RuntimeDownloadResult Cached(string installerPath, RuntimeDefinition definition) =>
        new(true, installerPath, true, definition, []);

    public static RuntimeDownloadResult Downloaded(string installerPath, RuntimeDefinition definition) =>
        new(true, installerPath, false, definition, []);

    public static RuntimeDownloadResult Failed(IReadOnlyList<DotSetupForge.Core.Models.DiagnosticMessage> errors) =>
        new(false, null, false, null, errors);
}
