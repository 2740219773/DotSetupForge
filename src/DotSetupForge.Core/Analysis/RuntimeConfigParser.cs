using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotSetupForge.Core.Analysis;

/// <summary>共享框架引用。</summary>
public sealed record FrameworkReference(string Name, string Version);

/// <summary>runtimeconfig.json 解析结果。</summary>
public sealed record RuntimeConfig(
    string Tfm,
    IReadOnlyList<FrameworkReference> Frameworks,
    string? RollForward,
    bool? ApplyPatches)
{
    public static readonly RuntimeConfig Empty = new(string.Empty, [], null, null);
}

/// <summary>解析 app.runtimeconfig.json。</summary>
public sealed class RuntimeConfigParser
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private sealed class RuntimeOptions
    {
        public string? Tfm { get; set; }

        public Framework? Framework { get; set; }

        public List<Framework>? Frameworks { get; set; }

        public string? RollForward { get; set; }

        public bool? ApplyPatches { get; set; }
    }

    private sealed class Framework
    {
        public string? Name { get; set; }

        public string? Version { get; set; }
    }

    private sealed class RuntimeConfigDocument
    {
        public RuntimeOptions? RuntimeOptions { get; set; }
    }

    /// <summary>解析 JSON 文本；失败时返回空结果（调用方通过文件存在性区分缺失）。</summary>
    public RuntimeConfig Parse(string json)
    {
        try
        {
            var doc = JsonSerializer.Deserialize<RuntimeConfigDocument>(json, Options);
            var options = doc?.RuntimeOptions;
            if (options is null)
            {
                return RuntimeConfig.Empty;
            }

            var frameworks = new List<FrameworkReference>();
            if (options.Framework is not null && !string.IsNullOrEmpty(options.Framework.Name))
            {
                frameworks.Add(new FrameworkReference(options.Framework.Name, options.Framework.Version ?? string.Empty));
            }

            if (options.Frameworks is not null)
            {
                foreach (var f in options.Frameworks)
                {
                    if (f.Name is not null)
                    {
                        frameworks.Add(new FrameworkReference(f.Name, f.Version ?? string.Empty));
                    }
                }
            }

            return new RuntimeConfig(
                options.Tfm ?? string.Empty,
                frameworks,
                options.RollForward,
                options.ApplyPatches);
        }
        catch (JsonException)
        {
            return RuntimeConfig.Empty;
        }
    }

    /// <summary>解析文件；文件缺失或畸形返回 null。</summary>
    public RuntimeConfig? ParseFile(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var result = Parse(File.ReadAllText(path));
        return result == RuntimeConfig.Empty ? null : result;
    }
}
