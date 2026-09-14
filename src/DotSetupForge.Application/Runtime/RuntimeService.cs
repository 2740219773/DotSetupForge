using DotSetupForge.Core.Runtime;
using DotSetupForge.Core.Models;

namespace DotSetupForge.Application.Runtime;

/// <summary>运行时管理应用层门面。</summary>
public sealed class RuntimeService
{
    private static readonly HttpClient Http = CreateHttpClient();
    private readonly RuntimeCache _cache = new();
    private readonly RuntimeDownloadManager _manager;
    private readonly LegacyFrameworkBootstrapperCatalog _legacyCatalog = new();
    private readonly LegacyFrameworkCache _legacyCache = new();

    /// <summary>使用官方在线元数据源。</summary>
    public RuntimeService()
        : this(new MicrosoftRuntimeCatalog(new HttpReleaseMetadataProvider(Http)))
    {
    }

    /// <summary>使用自定义目录（支持离线/内网镜像）。</summary>
    public RuntimeService(IRuntimeCatalog catalog)
    {
        _manager = new RuntimeDownloadManager(_cache, catalog, Http);
    }

    public IReadOnlyList<CachedRuntime> ListCached() => _cache.List();

    /// <summary>清空全部 Runtime 缓存。</summary>
    public void Clean() => _cache.Clear();

    /// <summary>删除单个缓存条目。</summary>
    public void RemoveCached(CachedRuntime entry) => _cache.Remove(entry);

    public Task<RuntimeDownloadResult> EnsureAsync(
        RuntimeRequirement requirement,
        IProgress<double>? progress = null,
        CancellationToken ct = default) =>
        _manager.EnsureAsync(requirement, progress, ct);

    /// <summary>校验并导入已手工下载的官方 Runtime 安装器。</summary>
    public Task<RuntimeDownloadResult> ImportAsync(
        RuntimeRequirement requirement,
        string installerFile,
        CancellationToken ct = default) =>
        _manager.ImportAsync(requirement, installerFile, ct);

    public LegacyFrameworkPackage? ResolveLegacyFramework(string version) => _legacyCatalog.Resolve(version);

    public CachedLegacyFramework? FindLegacyFramework(string version) => _legacyCache.Find(version);

    /// <summary>先检查专用缓存；若 SDK Bootstrapper 包目录中已有所需 EXE，则自动复制进缓存。</summary>
    public (LegacyFrameworkPackage? Package, CachedLegacyFramework? Cached, bool ImportedFromBootstrapper) EnsureLegacyFramework(string version)
    {
        var package = _legacyCatalog.Resolve(version);
        var cached = _legacyCache.Find(version);
        if (cached is not null || package is null || !Version.TryParse(version, out var requiredVersion))
        {
            return (package, cached, false);
        }

        // SDK 中常只带更高版本的 4.x 离线 EXE；它是就地升级，可满足旧应用。
        var localPackage = _legacyCatalog.List()
            .Where(candidate => Version.TryParse(candidate.Version, out var candidateVersion) &&
                                candidateVersion >= requiredVersion)
            .Select(_legacyCatalog.FindLocalInstallerPackage)
            .Where(candidate => candidate is not null)
            .Cast<LegacyFrameworkPackage>()
            .OrderBy(candidate => Version.Parse(candidate.Version))
            .FirstOrDefault();
        if (localPackage is null)
        {
            return (package, null, false);
        }

        try
        {
            return (localPackage, _legacyCache.Import(localPackage, localPackage.LocalInstallerPath), true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return (package, null, false);
        }
    }

    public (CachedLegacyFramework? Cached, DiagnosticMessage? Error) ImportLegacyFramework(string version, string installerFile)
    {
        var package = _legacyCatalog.ResolveForInstaller(version, installerFile);
        if (package is null)
        {
            return (null, DiagnosticMessage.Error("DP2011", $"所选文件不是 .NET Framework {version} 或更高版本的 ClickOnce Bootstrapper 离线安装器；请从 {LegacyFrameworkBootstrapperCatalog.DefaultPackagesDirectory} 选择 AllOS EXE。"));
        }

        try { return (_legacyCache.Import(package, installerFile), null); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or FileNotFoundException)
        {
            return (null, DiagnosticMessage.Error("DP2012", ex.Message));
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("DotSetupForge/0.1.0");
        return client;
    }
}
