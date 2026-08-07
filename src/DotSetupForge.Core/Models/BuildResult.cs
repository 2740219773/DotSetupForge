namespace DotSetupForge.Core.Models;

/// <summary>统一构建结果：业务错误通过 Errors 表达，不依赖异常。</summary>
public sealed class BuildResult
{
    public bool Success { get; set; }

    public List<DiagnosticMessage> Warnings { get; set; } = [];

    public List<DiagnosticMessage> Errors { get; set; } = [];

    /// <summary>产出文件路径。</summary>
    public List<string> Artifacts { get; set; } = [];

    public TimeSpan Duration { get; set; }

    public static BuildResult Ok(TimeSpan duration, params string[] artifacts) =>
        new() { Success = true, Duration = duration, Artifacts = [.. artifacts] };

    public static BuildResult Fail(IEnumerable<DiagnosticMessage> errors, TimeSpan duration) =>
        new() { Success = false, Errors = [.. errors], Duration = duration };
}
