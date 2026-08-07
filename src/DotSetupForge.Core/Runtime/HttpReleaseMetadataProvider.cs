namespace DotSetupForge.Core.Runtime;

/// <summary>通过官方 HTTP 源获取 releases 元数据。</summary>
public sealed class HttpReleaseMetadataProvider : IReleaseMetadataProvider
{
    public const string ReleaseMetadataBaseUrl =
        "https://dotnetcli.blob.core.windows.net/dotnet/release-metadata";

    private readonly HttpClient _http;

    public HttpReleaseMetadataProvider(HttpClient http) => _http = http;

    public async Task<string> GetReleasesJsonAsync(string majorMinor, CancellationToken ct = default)
    {
        var url = $"{ReleaseMetadataBaseUrl}/{majorMinor}/releases.json";
        return await _http.GetStringAsync(url, ct).ConfigureAwait(false);
    }
}
