using System.Text.Json;
using DotSetupForge.Core.Models;

namespace DotSetupForge.Core.Runtime;

/// <summary>旧 .NET Framework 离线安装器缓存；导入时复制原文件，绝不移动用户文件。</summary>
public sealed class LegacyFrameworkCache
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public LegacyFrameworkCache(string? rootDirectory = null) => RootDirectory = rootDirectory
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DotSetupForge", "Cache", "NetFramework");

    public string RootDirectory { get; }

    public CachedLegacyFramework? Find(string requiredVersion)
    {
        if (!Version.TryParse(requiredVersion, out var required) || !Directory.Exists(RootDirectory)) return null;
        return Directory.EnumerateFiles(RootDirectory, "metadata.json", SearchOption.AllDirectories)
            .Select(Read).Where(c => c is not null).Cast<CachedLegacyFramework>()
            .Where(c => File.Exists(c.InstallerPath) && Version.TryParse(c.Version, out var version) && version >= required)
            .OrderBy(c => Version.Parse(c.Version)).FirstOrDefault();
    }

    public CachedLegacyFramework Import(LegacyFrameworkPackage package, string installerFile)
    {
        if (!File.Exists(installerFile)) throw new FileNotFoundException("选择的 .NET Framework 安装文件不存在", installerFile);
        if (!string.Equals(Path.GetFileName(installerFile), package.InstallerFileName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"选择的文件不是 {package.DisplayName} 的离线安装器：应为 {package.InstallerFileName}");

        var directory = Path.Combine(RootDirectory, package.Version);
        Directory.CreateDirectory(directory);
        var cached = new CachedLegacyFramework(package.Version, package.ReleaseValue, package.InstallerFileName, directory);
        File.Copy(installerFile, cached.InstallerPath, overwrite: true);
        File.WriteAllText(Path.Combine(directory, "metadata.json"), JsonSerializer.Serialize(cached, JsonOptions));
        return cached;
    }

    private static CachedLegacyFramework? Read(string metadataFile)
    {
        try { return JsonSerializer.Deserialize<CachedLegacyFramework>(File.ReadAllText(metadataFile), JsonOptions); }
        catch (JsonException) { return null; }
        catch (IOException) { return null; }
    }
}
