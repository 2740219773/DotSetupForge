namespace DotSetupForge.Core.Models;

/// <summary>诊断消息严重级别。</summary>
public enum DiagnosticSeverity
{
    Info,
    Warning,
    Error,
}

/// <summary>统一诊断消息（带错误码，例如 DP1001）。</summary>
public record DiagnosticMessage
{
    /// <summary>错误码，例如 DP1001。</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>严重级别。</summary>
    public DiagnosticSeverity Severity { get; init; } = DiagnosticSeverity.Info;

    /// <summary>人类可读消息。</summary>
    public string Message { get; init; } = string.Empty;

    public static DiagnosticMessage Error(string code, string message) =>
        new() { Code = code, Severity = DiagnosticSeverity.Error, Message = message };

    public static DiagnosticMessage Warning(string code, string message) =>
        new() { Code = code, Severity = DiagnosticSeverity.Warning, Message = message };

    public static DiagnosticMessage Info(string message) =>
        new() { Code = string.Empty, Severity = DiagnosticSeverity.Info, Message = message };
}
