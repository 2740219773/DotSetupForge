using DotSetupForge.Application.Build;
using DotSetupForge.Core.Analysis;
using DotSetupForge.Core.Models;
using Xunit;

namespace DotSetupForge.IntegrationTests;

/// <summary>BuildService 端到端（本机未安装 ISCC 时验证错误路径 DP3002；模板生成在前置步骤验证）。</summary>
public class BuildServiceTests : IDisposable
{
    private readonly string _tempDir;

    public BuildServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"dsf-build-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        var sample = typeof(BuildServiceTests).Assembly.Location;
        File.Copy(sample, Path.Combine(_tempDir, "App.exe"));
        File.Copy(sample, Path.Combine(_tempDir, "App.dll"));
        File.WriteAllText(Path.Combine(_tempDir, "App.runtimeconfig.json"), """
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
        File.WriteAllText(Path.Combine(_tempDir, "App.deps.json"),
            """{ "runtimeTarget": { "name": ".NETCoreApp,Version=v10.0" } }""");
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
    public async Task Build_Without_Iscc_Should_Fail_With_DP3002()
    {
        var service = new BuildService();

        var result = await service.BuildAsync(new BuildRequest(_tempDir));

        // 本机未安装 Inno Setup：错误 DP3002；若已安装则编译成功或 DP3001
        if (new DotSetupForge.Inno.InnoSetupLocator().Locate().Found)
        {
            return; // 环境已装 Inno 时跳过断言
        }

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Code == "DP3002");
    }

    [Fact]
    public async Task Build_With_Missing_Directory_Should_Fail_With_DP1004()
    {
        var service = new BuildService();

        var result = await service.BuildAsync(new BuildRequest(Path.Combine(_tempDir, "not-exist")));

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Code == "DP1004");
    }
}
