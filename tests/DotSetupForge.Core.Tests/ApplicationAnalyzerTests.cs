using DotSetupForge.Core.Analysis;
using DotSetupForge.Core.Models;

namespace DotSetupForge.Core.Tests;

public class ApplicationAnalyzerTests : IDisposable
{
    private readonly ApplicationAnalyzer _analyzer = new();
    private readonly string _tempDir;

    public ApplicationAnalyzerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"dsf-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        catch (IOException)
        {
            // 忽略清理失败
        }
    }

    private string WriteFile(string name, string content)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, content);
        return path;
    }

    private static string RuntimeConfigJson() => """
        {
          "runtimeOptions": {
            "tfm": "net10.0",
            "frameworks": [
              { "name": "Microsoft.NETCore.App", "version": "10.0.0" },
              { "name": "Microsoft.WindowsDesktop.App", "version": "10.0.0" }
            ]
          }
        }
        """;

    private static string DepsJsonWpf() => """
        {
          "runtimeTarget": { "name": ".NETCoreApp,Version=v10.0", "signature": "" },
          "targets": {
            ".NETCoreApp,Version=v10.0": {
              "FakeApp/1.0.0": {
                "runtime": { "FakeApp.dll": {} }
              },
              "PresentationFramework/10.0.0": {
                "runtime": { "PresentationFramework.dll": {} }
              }
            }
          },
          "libraries": {
            "FakeApp/1.0.0": { "type": "project" },
            "PresentationFramework/10.0.0": { "type": "package" }
          }
        }
        """;

    private void CreateFakePublishDir()
    {
        var sample = typeof(ApplicationAnalyzerTests).Assembly.Location;
        File.Copy(sample, Path.Combine(_tempDir, "FakeApp.exe"));
        File.Copy(sample, Path.Combine(_tempDir, "FakeApp.dll"));
        WriteFile("FakeApp.runtimeconfig.json", RuntimeConfigJson());
        WriteFile("FakeApp.deps.json", DepsJsonWpf());
        WriteFile("appsettings.json", "{ }");
    }

    [Fact]
    public void Analyze_Should_Detect_Wpf_DesktopRuntime_And_FrameworkDependent()
    {
        CreateFakePublishDir();

        var result = _analyzer.Analyze(_tempDir);

        Assert.True(result.Success);
        Assert.Equal("FakeApp.exe", Path.GetFileName(result.MainExecutable));
        Assert.Equal("FakeApp", result.ApplicationName);
        Assert.Equal(ApplicationType.Wpf, result.ApplicationType);
        Assert.Equal("net10.0", result.TargetFramework);
        Assert.Equal("Microsoft.WindowsDesktop.App", result.FrameworkName);
        Assert.Equal(".NET Desktop Runtime", result.RuntimeName);
        Assert.Equal(DeploymentMode.FrameworkDependent, result.DeploymentMode);
    }

    [Fact]
    public void Analyze_MissingDirectory_Should_Fail()
    {
        var result = _analyzer.Analyze(Path.Combine(_tempDir, "not-exist"));

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code == "DP1004");
    }

    [Fact]
    public void Analyze_No_Exe_Should_Fail_With_DP1001()
    {
        WriteFile("data.json", "{}");

        var result = _analyzer.Analyze(_tempDir);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code == "DP1001");
    }

    [Fact]
    public void Analyze_Multiple_Candidates_Should_Fail_With_DP1005()
    {
        var sample = typeof(ApplicationAnalyzerTests).Assembly.Location;
        File.Copy(sample, Path.Combine(_tempDir, "Aaa.exe"));
        File.Copy(sample, Path.Combine(_tempDir, "Bbb.exe"));

        var result = _analyzer.Analyze(_tempDir);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code == "DP1005");
    }

    [Fact]
    public void Analyze_MainExecutable_Override_Should_Work()
    {
        CreateFakePublishDir();
        var sample = typeof(ApplicationAnalyzerTests).Assembly.Location;

        // 额外创建 Extra 四件套：不指定主程序时 resolver 会因多候选失败，override 应直接选中
        File.Copy(sample, Path.Combine(_tempDir, "Extra.exe"));
        File.Copy(sample, Path.Combine(_tempDir, "Extra.dll"));
        File.WriteAllText(Path.Combine(_tempDir, "Extra.runtimeconfig.json"), RuntimeConfigJson());
        File.WriteAllText(Path.Combine(_tempDir, "Extra.deps.json"), DepsJsonWpf());

        var result = _analyzer.Analyze(_tempDir, "Extra.exe");

        Assert.True(result.Success);
        Assert.Equal("Extra.exe", Path.GetFileName(result.MainExecutable));
    }
}
