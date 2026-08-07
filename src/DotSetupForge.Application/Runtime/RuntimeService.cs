using DotSetupForge.Core.Runtime;

namespace DotSetupForge.Application.Runtime;

/// <summary>运行时管理应用层门面。</summary>
public sealed class RuntimeService
{
    private static readonly HttpClient Http = CreateHttpClient();
    private readonly RuntimeCache _cache = new();
    private readonly RuntimeDownloadManager _manager;

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

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("DotSetupForge/0.1.0");
        return client;
    }
}
