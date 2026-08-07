namespace DotSetupForge.Core.Analysis;

/// <summary>文件类别。</summary>
public enum FileCategory
{
    Application,
    Library,
    Configuration,
    Data,
    Log,
    Debug,
    Runtime,
    Native,
    Unknown,
}

/// <summary>扫描到的文件。</summary>
public sealed record ScannedFile(
    string FileName,
    string RelativePath,
    long Size,
    string Extension,
    string Sha256,
    FileCategory Category);

/// <summary>程序类型。</summary>
public enum ApplicationType
{
    Wpf,
    WinForms,
    Console,
    Library,
    Unknown,
}

/// <summary>部署模式。</summary>
public enum DeploymentMode
{
    FrameworkDependent,
    SelfContained,
    SingleFile,
    Unknown,
}

/// <summary>主程序解析结果：多候选时 Resolved 为 null，Candidates 非空。</summary>
public sealed record ExecutableResolutionResult(
    string? Resolved,
    IReadOnlyList<string> Candidates);
