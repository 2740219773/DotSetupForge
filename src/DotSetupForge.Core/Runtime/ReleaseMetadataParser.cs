using System.Text.Json;
using DotSetupForge.Core.Models;

namespace DotSetupForge.Core.Runtime;

/// <summary>
/// 解析 .NET 官方 releases.json（release-metadata/{major.minor}/releases.json），
/// 定位指定 family / 版本 / 架构的运行时安装包。
/// </summary>
public sealed class ReleaseMetadataParser
{
    /// <summary>在 releases 元数据中寻找满足需求的最佳安装包定义。</summary>
    public RuntimeDefinition? FindBestMatch(string releasesJson, RuntimeRequirement requirement)
    {
        try
        {
            using var doc = JsonDocument.Parse(releasesJson);
            if (!doc.RootElement.TryGetProperty("releases", out var releases))
            {
                return null;
            }

            var (major, minor) = ParseMajorMinor(requirement.Version);
            var rid = ToRid(requirement.Architecture);

            // 收集同 major.minor 的所有 release，按版本降序，逐个找匹配文件（高版本缺失时回退低版本）
            var candidates = new List<JsonElement>();
            foreach (var release in releases.EnumerateArray())
            {
                if (!TryGetReleaseVersion(release, out var versionText) ||
                    !Version.TryParse(versionText, out var version))
                {
                    continue;
                }

                if (version.Major == major && version.Minor == minor)
                {
                    candidates.Add(release);
                }
            }

            foreach (var release in candidates
                         .OrderByDescending(r => Version.Parse(GetReleaseVersion(r))))
            {
                var definition = FindFileInRelease(release, requirement, rid);
                if (definition is not null)
                {
                    return definition;
                }
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static RuntimeDefinition? FindFileInRelease(
        JsonElement release,
        RuntimeRequirement requirement,
        string rid)
    {
        if (!TryGetReleaseVersion(release, out var versionText) ||
            !Version.TryParse(versionText, out var version))
        {
            return null;
        }

        var (familyField, filePrefix) = FamilyMapping(requirement.Family);
        if (!release.TryGetProperty(familyField, out var familyNode))
        {
            return null;
        }

        // windowsdesktop / aspnetcore-runtime 是数组；runtime 是对象
        IEnumerable<JsonElement> sections = familyNode.ValueKind == JsonValueKind.Array
            ? familyNode.EnumerateArray()
            : [familyNode];

        foreach (var section in sections)
        {
            if (!section.TryGetProperty("files", out var files))
            {
                continue;
            }

            foreach (var file in files.EnumerateArray())
            {
                if (!file.TryGetProperty("name", out var nameProp) ||
                    !file.TryGetProperty("url", out var urlProp))
                {
                    continue;
                }

                var name = nameProp.GetString() ?? string.Empty;
                if (!IsWindowsInstaller(name, filePrefix, rid))
                {
                    continue;
                }

                var hash = string.Empty;
                if (file.TryGetProperty("hash", out var hashProp))
                {
                    hash = hashProp.GetString() ?? string.Empty;
                }

                return new RuntimeDefinition(
                    requirement.Family,
                    versionText,
                    requirement.Architecture,
                    BuildDisplayName(requirement.Family, version, requirement.Architecture),
                    urlProp.GetString() ?? string.Empty,
                    name,
                    hash);
            }
        }

        return null;
    }

    /// <summary>兼容旧版 <c>version</c> 与当前官方元数据的 <c>release-version</c> 字段。</summary>
    private static bool TryGetReleaseVersion(JsonElement release, out string version)
    {
        foreach (var propertyName in new[] { "release-version", "version" })
        {
            if (release.TryGetProperty(propertyName, out var property) &&
                !string.IsNullOrWhiteSpace(property.GetString()))
            {
                version = property.GetString()!;
                return true;
            }
        }

        version = string.Empty;
        return false;
    }

    private static string GetReleaseVersion(JsonElement release)
    {
        _ = TryGetReleaseVersion(release, out var version);
        return version;
    }

    /// <summary>
    /// Microsoft release metadata has used both versioned installer names
    /// (<c>windowsdesktop-runtime-10.0.1-win-x64.exe</c>) and current RID-only
    /// names (<c>windowsdesktop-runtime-win-x64.exe</c>). The release version is
    /// authoritative in the parent release entry, so both forms are valid.
    /// </summary>
    private static bool IsWindowsInstaller(string name, string filePrefix, string rid) =>
        name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
        && (name.Equals($"{filePrefix}{rid}.exe", StringComparison.OrdinalIgnoreCase)
            || (name.StartsWith(filePrefix, StringComparison.OrdinalIgnoreCase)
                && name.Contains(rid, StringComparison.OrdinalIgnoreCase)));

    private static (int Major, int Minor) ParseMajorMinor(string version)
    {
        var v = Version.TryParse(version, out var parsed) ? parsed : new Version(0, 0);
        return (v.Major, v.Minor);
    }

    private static string ToRid(TargetArchitecture architecture) => architecture switch
    {
        TargetArchitecture.X86 => "win-x86",
        TargetArchitecture.Arm64 => "win-arm64",
        _ => "win-x64",
    };

    private static (string Field, string Prefix) FamilyMapping(RuntimeFamily family) => family switch
    {
        RuntimeFamily.DotNet => ("runtime", "dotnet-runtime-"),
        RuntimeFamily.WindowsDesktop => ("windowsdesktop", "windowsdesktop-runtime-"),
        RuntimeFamily.AspNetCore => ("aspnetcore-runtime", "aspnetcore-runtime-"),
        _ => ("runtime", "dotnet-runtime-"),
    };

    private static string BuildDisplayName(RuntimeFamily family, Version version, TargetArchitecture arch)
    {
        var familyName = family switch
        {
            RuntimeFamily.DotNet => ".NET Runtime",
            RuntimeFamily.WindowsDesktop => ".NET Desktop Runtime",
            RuntimeFamily.AspNetCore => "ASP.NET Core Runtime",
            _ => "Runtime",
        };
        return $"{familyName} {version} {arch}";
    }
}
