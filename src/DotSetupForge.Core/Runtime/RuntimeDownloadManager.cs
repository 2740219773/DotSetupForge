using System.Security.Cryptography;
using DotSetupForge.Core.Models;

namespace DotSetupForge.Core.Runtime;

/// <summary>运行时下载管理器：缓存命中 → 下载 → SHA256 校验 → 落入缓存。支持进度、取消、超时、失败重试。</summary>
public sealed class RuntimeDownloadManager
{
    private const int MaxRetries = 3;

    private readonly RuntimeCache _cache;
    private readonly IRuntimeCatalog _catalog;
    private readonly HttpClient _http;

    public RuntimeDownloadManager(RuntimeCache cache, IRuntimeCatalog catalog, HttpClient http)
    {
        _cache = cache;
        _catalog = catalog;
        _http = http;
    }

    /// <summary>确保运行时已缓存：命中直接返回，否则下载、校验、缓存。</summary>
    public async Task<RuntimeDownloadResult> EnsureAsync(
        RuntimeRequirement requirement,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        // 1. 缓存命中
        var cached = _cache.Find(requirement);
        if (cached is not null)
        {
            return RuntimeDownloadResult.Cached(cached.InstallerPath, cached.ToDefinition());
        }

        // 2. 解析下载定义
        RuntimeDefinition? definition;
        try
        {
            definition = await _catalog.ResolveAsync(requirement, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            return RuntimeDownloadResult.Failed(
                [DiagnosticMessage.Error("DP2003", $"无法获取运行时元数据：{ex.Message}")]);
        }

        if (definition is null || string.IsNullOrEmpty(definition.DownloadUrl))
        {
            return RuntimeDownloadResult.Failed(
                [DiagnosticMessage.Error("DP2003", $"无法解析 {requirement.Family} {requirement.Version} {requirement.Architecture} 的下载地址")]);
        }

        // 3. 下载（带重试）
        var workDir = Path.Combine(Path.GetTempPath(), "DotSetupForge", $"runtime-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workDir);

        try
        {
            var installerFile = Path.Combine(workDir, definition.FileName);
            await DownloadWithRetryAsync(definition, installerFile, progress, ct).ConfigureAwait(false);

            // 4. 校验
            var expected = definition.Sha256;
            if (!string.IsNullOrEmpty(expected))
            {
                var actual = await ComputeHashAsync(installerFile, expected, ct).ConfigureAwait(false);
                if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
                {
                    return RuntimeDownloadResult.Failed(
                        [DiagnosticMessage.Error("DP2002",
                            $".NET Runtime 文件校验失败：期望 {expected}，实际 {actual}")]);
                }
            }

            // 5. 落入缓存
            var saved = _cache.Save(definition, installerFile);
            return RuntimeDownloadResult.Downloaded(saved.InstallerPath, definition);
        }
        finally
        {
            try
            {
                Directory.Delete(workDir, recursive: true);
            }
            catch (IOException)
            {
                // 忽略清理失败
            }
        }
    }

    /// <summary>
    /// 导入用户已经下载的官方 Runtime 安装器。始终依据当前官方元数据校验摘要，
    /// 且只复制源文件，绝不移动或修改用户保留的安装器。
    /// </summary>
    public async Task<RuntimeDownloadResult> ImportAsync(
        RuntimeRequirement requirement,
        string installerFile,
        CancellationToken ct = default)
    {
        if (!File.Exists(installerFile))
        {
            return RuntimeDownloadResult.Failed(
                [DiagnosticMessage.Error("DP2001", "选择的 Runtime 安装文件不存在")]);
        }

        RuntimeDefinition? definition;
        try
        {
            definition = await _catalog.ResolveAsync(requirement, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            return RuntimeDownloadResult.Failed(
                [DiagnosticMessage.Error("DP2003", $"无法获取运行时元数据：{ex.Message}")]);
        }

        if (definition is null || string.IsNullOrEmpty(definition.Sha256))
        {
            return RuntimeDownloadResult.Failed(
                [DiagnosticMessage.Error("DP2003", $"无法解析 {requirement.Family} {requirement.Version} {requirement.Architecture} 的校验信息")]);
        }

        var workDir = Path.Combine(Path.GetTempPath(), "DotSetupForge", $"runtime-import-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workDir);

        try
        {
            var copiedInstaller = Path.Combine(workDir, definition.FileName);
            File.Copy(installerFile, copiedInstaller, overwrite: true);

            var actual = await ComputeHashAsync(copiedInstaller, definition.Sha256, ct).ConfigureAwait(false);
            if (!actual.Equals(definition.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                return RuntimeDownloadResult.Failed(
                    [DiagnosticMessage.Error("DP2002",
                        $"本地 Runtime 文件校验失败：期望 {definition.Sha256}，实际 {actual}")]);
            }

            var saved = _cache.Save(definition, copiedInstaller);
            return RuntimeDownloadResult.Downloaded(saved.InstallerPath, definition);
        }
        finally
        {
            try
            {
                Directory.Delete(workDir, recursive: true);
            }
            catch (IOException)
            {
                // 忽略清理失败
            }
        }
    }

    private async Task DownloadWithRetryAsync(
        RuntimeDefinition definition,
        string destination,
        IProgress<double>? progress,
        CancellationToken ct)
    {
        Exception? lastError = null;

        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                using var response = await _http.GetAsync(
                    definition.DownloadUrl,
                    HttpCompletionOption.ResponseHeadersRead,
                    ct).ConfigureAwait(false);

                response.EnsureSuccessStatusCode();

                var total = response.Content.Headers.ContentLength ?? -1;
                await using var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                await using var target = File.Create(destination);

                var buffer = new byte[81920];
                long copied = 0;
                int read;
                while ((read = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                    copied += read;
                    if (total > 0)
                    {
                        progress?.Report((double)copied / total);
                    }
                }

                return; // 成功
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException)
            {
                lastError = ex;
                if (ct.IsCancellationRequested || attempt == MaxRetries)
                {
                    break;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(500 * attempt), ct).ConfigureAwait(false);
            }
        }

        throw lastError ?? new HttpRequestException("下载失败");
    }

    /// <summary>
    /// 官方 release metadata 的 hash 既可能是 SHA-256（64 个十六进制字符），
    /// 也可能是 SHA-512（128 个十六进制字符）。按声明摘要长度选择算法，
    /// 其他长度保持 SHA-256 比对并由调用方报告校验失败。
    /// </summary>
    private static async Task<string> ComputeHashAsync(
        string path,
        string expectedHash,
        CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        var hash = expectedHash.Length == 128
            ? await SHA512.HashDataAsync(stream, ct).ConfigureAwait(false)
            : await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }
}
