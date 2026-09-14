using System.Net;
using System.Security.Cryptography;
using System.Text;
using DotSetupForge.Core.Models;
using DotSetupForge.Core.Runtime;

namespace DotSetupForge.Core.Tests;

public class RuntimeDownloadManagerTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly RuntimeCache _cache;

    public RuntimeDownloadManagerTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"dsf-dl-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
        _cache = new RuntimeCache(_tempRoot);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
        catch (IOException)
        {
            // 忽略
        }
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly byte[] _content;
        public int CallCount;

        public StubHttpMessageHandler(byte[] content) => _content = content;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(_content),
            });
        }
    }

    private sealed class FakeCatalog : IRuntimeCatalog
    {
        private readonly RuntimeDefinition? _definition;
        public FakeCatalog(RuntimeDefinition? definition) => _definition = definition;

        public Task<RuntimeDefinition?> ResolveAsync(
            RuntimeRequirement requirement, CancellationToken ct = default) =>
            Task.FromResult(_definition);
    }

    private static string Sha256Hex(byte[] content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    private static string Sha512Hex(byte[] content) =>
        Convert.ToHexString(SHA512.HashData(content)).ToLowerInvariant();

    private static RuntimeDefinition Definition(string sha256) => new(
        RuntimeFamily.WindowsDesktop,
        "10.0.1",
        TargetArchitecture.X64,
        ".NET Desktop Runtime 10.0.1 X64",
        "https://example/windowsdesktop-runtime-10.0.1-win-x64.exe",
        "windowsdesktop-runtime-10.0.1-win-x64.exe",
        sha256);

    private static RuntimeRequirement Requirement() => new(
        RuntimeFamily.WindowsDesktop, "10.0", TargetArchitecture.X64);

    private static HttpClient Client(StubHttpMessageHandler handler) => new(handler);

    [Fact]
    public async Task Ensure_CacheHit_Should_Return_FromCache()
    {
        var content = Encoding.UTF8.GetBytes("runtime-installer-bytes");
        var seed = Path.Combine(_tempRoot, "pre-seeded.exe");
        File.WriteAllBytes(seed, content);
        _cache.Save(Definition(Sha256Hex(content)), seed);

        var manager = new RuntimeDownloadManager(_cache, new FakeCatalog(null), Client(new StubHttpMessageHandler([])));

        var result = await manager.EnsureAsync(Requirement());

        Assert.True(result.Success);
        Assert.True(result.FromCache);
        Assert.NotNull(result.InstallerPath);
        Assert.True(File.Exists(result.InstallerPath));
    }

    [Fact]
    public async Task Ensure_Download_Success_Should_Save_To_Cache_Then_Hit()
    {
        var content = Encoding.UTF8.GetBytes("runtime-installer-bytes");
        var handler = new StubHttpMessageHandler(content);
        var manager = new RuntimeDownloadManager(
            _cache, new FakeCatalog(Definition(Sha256Hex(content))), Client(handler));

        var first = await manager.EnsureAsync(Requirement());

        Assert.True(first.Success);
        Assert.False(first.FromCache);
        Assert.True(File.Exists(first.InstallerPath));
        Assert.Equal(1, handler.CallCount);

        // 第二次：直接命中缓存，不重新下载
        var second = await manager.EnsureAsync(Requirement());

        Assert.True(second.Success);
        Assert.True(second.FromCache);
        Assert.Equal(1, handler.CallCount);
        Assert.Equal(first.InstallerPath, second.InstallerPath);
    }

    [Fact]
    public async Task Ensure_Download_With_Sha512_MetadataHash_Should_Save_To_Cache()
    {
        var content = Encoding.UTF8.GetBytes("runtime-installer-bytes");
        var manager = new RuntimeDownloadManager(
            _cache, new FakeCatalog(Definition(Sha512Hex(content))), Client(new StubHttpMessageHandler(content)));

        var result = await manager.EnsureAsync(Requirement());

        Assert.True(result.Success);
        Assert.False(result.FromCache);
        Assert.True(File.Exists(result.InstallerPath));
    }

    [Fact]
    public async Task Ensure_HashMismatch_Should_Fail_With_DP2002()
    {
        var content = Encoding.UTF8.GetBytes("runtime-installer-bytes");
        var manager = new RuntimeDownloadManager(
            _cache, new FakeCatalog(Definition("WRONG-HASH")), Client(new StubHttpMessageHandler(content)));

        var result = await manager.EnsureAsync(Requirement());

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Code == "DP2002");
    }

    [Fact]
    public async Task Ensure_Unresolvable_Catalog_Should_Fail_With_DP2003()
    {
        var manager = new RuntimeDownloadManager(
            _cache, new FakeCatalog(null), Client(new StubHttpMessageHandler([])));

        var result = await manager.EnsureAsync(Requirement());

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Code == "DP2003");
    }

    [Fact]
    public async Task Import_Valid_Local_Installer_Should_Verify_Copy_And_Save_To_Cache()
    {
        var content = Encoding.UTF8.GetBytes("runtime-installer-bytes");
        var source = Path.Combine(_tempRoot, "manual-runtime.exe");
        File.WriteAllBytes(source, content);
        var manager = new RuntimeDownloadManager(
            _cache, new FakeCatalog(Definition(Sha256Hex(content))), Client(new StubHttpMessageHandler([])));

        var result = await manager.ImportAsync(Requirement(), source);

        Assert.True(result.Success);
        Assert.True(File.Exists(source));
        Assert.NotNull(result.InstallerPath);
        Assert.True(File.Exists(result.InstallerPath));
    }

    [Fact]
    public async Task Import_HashMismatch_Should_Fail_Without_Caching_File()
    {
        var source = Path.Combine(_tempRoot, "manual-runtime.exe");
        File.WriteAllBytes(source, Encoding.UTF8.GetBytes("untrusted-bytes"));
        var manager = new RuntimeDownloadManager(
            _cache, new FakeCatalog(Definition("ABCDEF")), Client(new StubHttpMessageHandler([])));

        var result = await manager.ImportAsync(Requirement(), source);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Code == "DP2002");
        Assert.Empty(_cache.List());
    }
}
