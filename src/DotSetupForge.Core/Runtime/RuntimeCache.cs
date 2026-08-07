using System.Text.Json;
using DotSetupForge.Core.Models;

namespace DotSetupForge.Core.Runtime;

/// <summary>缓存中的运行时条目。</summary>
public sealed record CachedRuntime(
    RuntimeFamily Family,
    string Version,
    TargetArchitecture Architecture,
    string FileName,
    string Url,
    string Sha256,
    DateTime DownloadedAt,
    string Directory)
{
    public string InstallerPath => Path.Combine(Directory, FileName);

    /// <summary>还原为安装包定义（供 prerequisite 构建使用）。</summary>
    public RuntimeDefinition ToDefinition() => new(
        Family, Version, Architecture,
        $"{Family} {Version} {Architecture}",
        Url, FileName, Sha256);
}

/// <summary>
/// 运行时本地缓存：
/// %LOCALAPPDATA%\DotSetupForge\Cache\Runtime\{family}\{version}\{arch}\
/// 每个条目旁保存 metadata.json（URL / SHA256 / DownloadedAt / Version / Architecture）。
/// </summary>
public sealed class RuntimeCache
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    public string RootDirectory { get; }

    public RuntimeCache(string? rootDirectory = null)
    {
        RootDirectory = rootDirectory
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DotSetupForge", "Cache", "Runtime");
    }

    /// <summary>列出所有缓存条目。</summary>
    public IReadOnlyList<CachedRuntime> List()
    {
        if (!Directory.Exists(RootDirectory))
        {
            return [];
        }

        var result = new List<CachedRuntime>();
        foreach (var metadataFile in Directory.EnumerateFiles(RootDirectory, "metadata.json", SearchOption.AllDirectories))
        {
            try
            {
                var cached = JsonSerializer.Deserialize<CachedRuntime>(File.ReadAllText(metadataFile), JsonOptions);
                if (cached is not null && File.Exists(cached.InstallerPath))
                {
                    result.Add(cached);
                }
            }
            catch (JsonException)
            {
                // 损坏的 metadata 跳过
            }
        }

        return result.OrderBy(c => c.Family).ThenBy(c => c.Version).ThenBy(c => c.Architecture).ToList();
    }

    /// <summary>
    /// 查找满足需求的缓存：同 Family/Arch，且缓存版本 major.minor 与需求一致（Patch 向上兼容），取最高 patch。
    /// </summary>
    public CachedRuntime? Find(RuntimeRequirement requirement)
    {
        var (major, minor) = ParseMajorMinor(requirement.Version);

        return List()
            .Where(c => c.Family == requirement.Family
                && c.Architecture == requirement.Architecture
                && SameMajorMinor(c.Version, major, minor))
            .OrderByDescending(c => Version.TryParse(c.Version, out var v) ? v : new Version(0, 0))
            .FirstOrDefault();
    }

    /// <summary>把已下载并校验的安装包落入缓存并写 metadata.json。</summary>
    public CachedRuntime Save(RuntimeDefinition definition, string installerFile)
    {
        var entryDir = Path.Combine(
            RootDirectory,
            definition.Family.ToString(),
            definition.Version,
            definition.Architecture.ToString());

        Directory.CreateDirectory(entryDir);

        var targetPath = Path.Combine(entryDir, definition.FileName);
        File.Move(installerFile, targetPath, overwrite: true);

        var cached = new CachedRuntime(
            definition.Family,
            definition.Version,
            definition.Architecture,
            definition.FileName,
            definition.DownloadUrl,
            definition.Sha256,
            DateTime.UtcNow,
            entryDir);

        File.WriteAllText(
            Path.Combine(entryDir, "metadata.json"),
            JsonSerializer.Serialize(cached, JsonOptions));

        return cached;
    }

    private static (int Major, int Minor) ParseMajorMinor(string version)
    {
        var v = Version.TryParse(version, out var parsed) ? parsed : new Version(0, 0);
        return (v.Major, v.Minor);
    }

    private static bool SameMajorMinor(string version, int major, int minor) =>
        Version.TryParse(version, out var v) && v.Major == major && v.Minor == minor;
}
