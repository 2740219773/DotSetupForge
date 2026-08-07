using DotSetupForge.Core.Models;
using DotSetupForge.Core.Runtime;

namespace DotSetupForge.Core.Tests;

public class MicrosoftRuntimeCatalogTests : IDisposable
{
    private readonly string _tempRoot;

    public MicrosoftRuntimeCatalogTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"dsf-catalog-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_tempRoot, "10.0"));
        File.Copy(
            Path.Combine(AppContext.BaseDirectory, "fixtures", "releases-10.0.json"),
            Path.Combine(_tempRoot, "10.0", "releases.json"));
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

    [Fact]
    public async Task ResolveAsync_OfflineMetadata_Should_Return_Definition()
    {
        var catalog = new MicrosoftRuntimeCatalog(new FileReleaseMetadataProvider(_tempRoot));

        var definition = await catalog.ResolveAsync(new RuntimeRequirement(
            RuntimeFamily.WindowsDesktop, "10.0", TargetArchitecture.X64));

        Assert.NotNull(definition);
        Assert.Equal("10.0.1", definition!.Version);
        Assert.Equal("windowsdesktop-runtime-10.0.1-win-x64.exe", definition.FileName);
        Assert.Equal("DDD", definition.Sha256);
    }

    [Fact]
    public async Task ResolveAsync_Missing_OfflineMetadata_Should_Throw()
    {
        var catalog = new MicrosoftRuntimeCatalog(new FileReleaseMetadataProvider(_tempRoot));

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            catalog.ResolveAsync(new RuntimeRequirement(
                RuntimeFamily.WindowsDesktop, "9.0", TargetArchitecture.X64)));
    }
}
