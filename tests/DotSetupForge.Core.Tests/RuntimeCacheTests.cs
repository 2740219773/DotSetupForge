using DotSetupForge.Core.Models;
using DotSetupForge.Core.Runtime;

namespace DotSetupForge.Core.Tests;

public class RuntimeCacheTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly RuntimeCache _cache;

    public RuntimeCacheTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"dsf-cache-{Guid.NewGuid():N}");
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

    private static RuntimeDefinition Definition(string version, TargetArchitecture arch, string hash) =>
        new(
            RuntimeFamily.WindowsDesktop,
            version,
            arch,
            $".NET Desktop Runtime {version} {arch}",
            $"https://example/{version}-{arch}.exe",
            $"windowsdesktop-runtime-{version}-win-{(arch == TargetArchitecture.X86 ? "x86" : "x64")}.exe",
            hash);

    private void SaveSample(string version, TargetArchitecture arch)
    {
        var installer = Path.Combine(_tempRoot, $"seed-{version}-{arch}.exe");
        File.WriteAllBytes(installer, [1, 2, 3]);
        _cache.Save(Definition(version, arch, "HASH"), installer);
    }

    [Fact]
    public void Save_Then_List_Should_Return_Entry()
    {
        SaveSample("10.0.1", TargetArchitecture.X64);

        var list = _cache.List();

        var entry = Assert.Single(list);
        Assert.Equal(RuntimeFamily.WindowsDesktop, entry.Family);
        Assert.Equal("10.0.1", entry.Version);
        Assert.Equal("HASH", entry.Sha256);
    }

    [Fact]
    public void Find_Same_MajorMinor_Should_Hit_Latest_Patch()
    {
        SaveSample("10.0.0", TargetArchitecture.X64);
        SaveSample("10.0.1", TargetArchitecture.X64);

        var hit = _cache.Find(new RuntimeRequirement(
            RuntimeFamily.WindowsDesktop, "10.0", TargetArchitecture.X64));

        Assert.NotNull(hit);
        Assert.Equal("10.0.1", hit!.Version); // Patch 向上兼容，取最高
    }

    [Fact]
    public void Find_Different_Major_Should_Miss()
    {
        SaveSample("9.0.0", TargetArchitecture.X64);

        var hit = _cache.Find(new RuntimeRequirement(
            RuntimeFamily.WindowsDesktop, "10.0", TargetArchitecture.X64));

        Assert.Null(hit);
    }

    [Fact]
    public void Find_Different_Architecture_Should_Miss()
    {
        SaveSample("10.0.1", TargetArchitecture.X64);

        var hit = _cache.Find(new RuntimeRequirement(
            RuntimeFamily.WindowsDesktop, "10.0", TargetArchitecture.X86));

        Assert.Null(hit);
    }

    [Fact]
    public void Save_Moves_File_And_Writes_Metadata()
    {
        var installer = Path.Combine(_tempRoot, "seed-installer.exe");
        File.WriteAllBytes(installer, [9, 9]);

        _cache.Save(Definition("10.0.1", TargetArchitecture.X64, "HASH"), installer);

        var entryDir = Path.Combine(
            _tempRoot,
            RuntimeFamily.WindowsDesktop.ToString(),
            "10.0.1",
            TargetArchitecture.X64.ToString());
        Assert.True(File.Exists(Path.Combine(entryDir, "metadata.json")));
        Assert.True(File.Exists(Path.Combine(entryDir, "windowsdesktop-runtime-10.0.1-win-x64.exe")));
        Assert.False(File.Exists(installer)); // 源文件已移动
    }
}
