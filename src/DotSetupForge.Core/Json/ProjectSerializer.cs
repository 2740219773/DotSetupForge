using System.Text.Json;
using System.Text.Json.Serialization;
using DotSetupForge.Core.Models;

namespace DotSetupForge.Core.Json;

/// <summary>反序列化结果：畸形 JSON 时 Project 为 null 且 Errors 非空，不抛异常。</summary>
public sealed record ProjectLoadResult(PackageProject? Project, IReadOnlyList<DiagnosticMessage> Errors)
{
    public bool Success => Project is not null;
}

/// <summary>PackageProject 的 JSON 序列化/反序列化。</summary>
public static class ProjectSerializer
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static JsonSerializerOptions CreateOptions() => new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>序列化为 JSON 字符串。</summary>
    public static string Serialize(PackageProject project) =>
        JsonSerializer.Serialize(project, Options);

    /// <summary>反序列化：失败时返回错误诊断而非抛异常。</summary>
    public static ProjectLoadResult Deserialize(string json)
    {
        try
        {
            var project = JsonSerializer.Deserialize<PackageProject>(json, Options);
            return project is null
                ? new ProjectLoadResult(null, [DiagnosticMessage.Error("DP1000", "配置内容为空")])
                : new ProjectLoadResult(project, []);
        }
        catch (JsonException ex)
        {
            return new ProjectLoadResult(null,
                [DiagnosticMessage.Error("DP1000", $"配置解析失败：{ex.Message}")]);
        }
    }
}
