using System.Text.Json;

namespace DotSetupForge.Core.Analysis;

/// <summary>deps.json 分析结果。</summary>
public sealed record DepsJsonInfo(
    string RuntimeTarget,
    IReadOnlyList<string> Rids,
    IReadOnlyList<string> NativeAssets,
    IReadOnlyList<string> ReferencedAssemblies)
{
    public static readonly DepsJsonInfo Empty = new(string.Empty, [], [], []);
}

/// <summary>解析 app.deps.json：提取 runtimeTarget、RID、native 依赖。</summary>
public sealed class DepsJsonAnalyzer
{
    public DepsJsonInfo Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var runtimeTarget = string.Empty;
            if (root.TryGetProperty("runtimeTarget", out var rt) &&
                rt.TryGetProperty("name", out var name))
            {
                runtimeTarget = name.GetString() ?? string.Empty;
            }

            var rids = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            var nativeAssets = new List<string>();
            var referencedAssemblies = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            if (root.TryGetProperty("targets", out var targets))
            {
                foreach (var target in targets.EnumerateObject())
                {
                    foreach (var lib in target.Value.EnumerateObject())
                    {
                        if (lib.Value.TryGetProperty("runtime", out var runtime))
                        {
                            foreach (var asset in runtime.EnumerateObject())
                            {
                                referencedAssemblies.Add(Path.GetFileName(asset.Name));
                            }
                        }

                        if (lib.Value.TryGetProperty("runtimeTargets", out var runtimeTargets))
                        {
                            foreach (var asset in runtimeTargets.EnumerateObject())
                            {
                                var rid = string.Empty;
                                if (asset.Value.TryGetProperty("rid", out var ridProp))
                                {
                                    rid = ridProp.GetString() ?? string.Empty;
                                }
                                else
                                {
                                    // 回退：从路径 runtimes/{rid}/... 提取
                                    var segments = asset.Name.Split('/');
                                    if (segments.Length > 1 && segments[0].Equals("runtimes", StringComparison.OrdinalIgnoreCase))
                                    {
                                        rid = segments[1];
                                    }
                                }

                                if (!string.IsNullOrEmpty(rid))
                                {
                                    rids.Add(rid);
                                }

                                var assetType = string.Empty;
                                if (asset.Value.TryGetProperty("assetType", out var typeProp))
                                {
                                    assetType = typeProp.GetString() ?? string.Empty;
                                }

                                if (assetType.Equals("native", StringComparison.OrdinalIgnoreCase))
                                {
                                    nativeAssets.Add(asset.Name);
                                }
                            }
                        }
                    }
                }
            }

            return new DepsJsonInfo(runtimeTarget, [.. rids], nativeAssets, [.. referencedAssemblies]);
        }
        catch (JsonException)
        {
            return DepsJsonInfo.Empty;
        }
    }

    public DepsJsonInfo? ParseFile(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var result = Parse(File.ReadAllText(path));
        return result == DepsJsonInfo.Empty ? null : result;
    }
}
