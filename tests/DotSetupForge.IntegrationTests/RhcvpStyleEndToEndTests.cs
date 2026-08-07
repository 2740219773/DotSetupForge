using DotSetupForge.Application.Analysis;
using DotSetupForge.Application.Packaging;
using DotSetupForge.Core.Analysis;
using DotSetupForge.Core.Models;
using DotSetupForge.Core.Packaging;
using Xunit;

namespace DotSetupForge.IntegrationTests;

/// <summary>模拟 RHCVP 发布目录（四件套 + Data/Logs/runtimes + 第三方 DLL + appsettings + pdb）的端到端链路：
/// analyze → InstallerModelBuilder。</summary>
public class RhcvpStyleEndToEndTests : IDisposable
{
    private readonly string _tempDir;

    public RhcvpStyleEndToEndTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"dsf-rhcvp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_tempDir, "Data"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "Logs"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "runtimes", "win-x64", "native"));

        var sample = typeof(RhcvpStyleEndToEndTests).Assembly.Location;

        File.Copy(sample, Path.Combine(_tempDir, "RHCVP.exe"));
        File.Copy(sample, Path.Combine(_tempDir, "RHCVP.dll"));
        File.Copy(sample, Path.Combine(_tempDir, "SkiaSharp.dll"));
        File.Copy(sample, Path.Combine(_tempDir, "runtimes", "win-x64", "native", "libSkiaSharp.dll"));

        File.WriteAllText(Path.Combine(_tempDir, "RHCVP.runtimeconfig.json"), """
            {
              "runtimeOptions": {
                "tfm": "net10.0",
                "frameworks": [
                  { "name": "Microsoft.NETCore.App", "version": "10.0.0" },
                  { "name": "Microsoft.WindowsDesktop.App", "version": "10.0.0" }
                ]
              }
            }
            """);

        File.WriteAllText(Path.Combine(_tempDir, "RHCVP.deps.json"), """
            {
              "runtimeTarget": { "name": ".NETCoreApp,Version=v10.0" },
              "targets": { ".NETCoreApp,Version=v10.0": { "RHCVP/1.0.0": { "runtime": { "RHCVP.dll": {} } } } },
              "libraries": { "RHCVP/1.0.0": { "type": "project" } }
            }
            """);

        File.WriteAllText(Path.Combine(_tempDir, "appsettings.json"), "{ }");
        File.WriteAllText(Path.Combine(_tempDir, "Data", "config.dat"), "data");
        File.WriteAllText(Path.Combine(_tempDir, "Logs", "app.log"), "log");
        File.WriteAllBytes(Path.Combine(_tempDir, "RHCVP.pdb"), [1]);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        catch (IOException)
        {
            // 忽略
        }
    }

    [Fact]
    public void Analyze_Then_Build_Should_Produce_InstallerModel()
    {
        var analysis = new ApplicationAnalysisService().Analyze(_tempDir);

        Assert.True(analysis.Success);
        Assert.Equal("RHCVP.exe", Path.GetFileName(analysis.MainExecutable));
        Assert.Equal(ApplicationType.Wpf, analysis.ApplicationType);

        var model = new InstallerModelBuilder().Build(analysis);

        // 产品
        Assert.Equal("RHCVP", model.Product.Name);
        Assert.Equal("RHCVP.exe", model.MainExecutable);

        // 文件：排除 pdb 与 Logs，保留 exe/dll/json/Data/runtimes
        var relativePaths = model.Files.Select(f => f.SourceRelativePath).ToList();
        Assert.Contains("RHCVP.exe", relativePaths);
        Assert.Contains("SkiaSharp.dll", relativePaths);
        Assert.Contains("appsettings.json", relativePaths);
        Assert.Contains("Data/config.dat", relativePaths);
        Assert.Contains("runtimes/win-x64/native/libSkiaSharp.dll", relativePaths);
        Assert.DoesNotContain(relativePaths, p => p.EndsWith(".pdb"));
        Assert.DoesNotContain(relativePaths, p => p.StartsWith("Logs/"));

        // 策略
        var config = model.Files.Single(f => f.SourceRelativePath == "appsettings.json");
        Assert.Equal(UpgradePolicy.PreserveExisting, config.UpgradePolicy);

        var native = model.Files.Single(f => f.SourceRelativePath.StartsWith("runtimes/"));
        Assert.Equal("runtimes/win-x64/native", native.TargetSubDirectory);

        // 快捷方式
        Assert.Equal(2, model.Shortcuts.Count);
    }
}
