namespace DotSetupForge.Core.Models;

/// <summary>统一构建结果：业务错误通过 Errors 表达，不依赖异常。</summary>
public record BuildResult
{
    public bool Success { get; init; }

    public List<DiagnosticMessage> Warnings { get; init; } = [];

    public List<DiagnosticMessage> Errors { get; init; } = [];

    /// <summary>产出文件路径。</summary>
    public List<string> Artifacts { get; init; } = [];

    public TimeSpan Duration { get; init; }

    public static BuildResult Ok(TimeSpan duration, params string[] artifacts) =>
        new() { Success = true, Duration = duration, Artifacts = [.. artifacts] };

    public static BuildResult Fail(IEnumerable<DiagnosticMessage> errors, TimeSpan duration) =>
        new() { Success = false, Errors = [.. errors], Duration = duration };
}
