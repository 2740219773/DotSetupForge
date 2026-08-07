namespace DotSetupForge.Core.Runtime;

/// <summary>微软官方运行时目录：releases.json → 匹配 family/version/arch 的安装包。</summary>
public sealed class MicrosoftRuntimeCatalog : IRuntimeCatalog
{
    private readonly IReleaseMetadataProvider _provider;
    private readonly ReleaseMetadataParser _parser = new();

    public MicrosoftRuntimeCatalog(IReleaseMetadataProvider provider) => _provider = provider;

    public async Task<RuntimeDefinition?> ResolveAsync(
        RuntimeRequirement requirement,
        CancellationToken ct = default)
    {
        var majorMinor = GetMajorMinor(requirement.Version);
        if (majorMinor is null)
        {
            return null;
        }

        var json = await _provider.GetReleasesJsonAsync(majorMinor, ct).ConfigureAwait(false);
        return _parser.FindBestMatch(json, requirement);
    }

    internal static string? GetMajorMinor(string version)
    {
        if (!System.Version.TryParse(version, out var v))
        {
            return null;
        }
        return $"{v.Major}.{v.Minor}";
    }
}
