using DotSetupForge.Core.Json;
using DotSetupForge.Core.Models;

namespace DotSetupForge.Application.Projects;

/// <summary>打包项目的应用层门面：CLI / GUI 只依赖本层。</summary>
public sealed class PackageProjectService
{
    /// <summary>创建测试用打包项目。</summary>
    public PackageProject CreateTestProject() => new()
    {
        SchemaVersion = PackageProject.CurrentSchemaVersion,
        Product = new ProductInfo
        {
            AppId = Guid.NewGuid(),
            Name = "TestApp",
            Version = "1.0.0",
            Publisher = "Test Publisher",
            MainExecutable = "TestApp.exe",
        },
        Source = new SourceInfo
        {
            Type = SourceType.Directory,
            Path = "./bin/Release/net10.0-windows",
            Configuration = "Release",
        },
        Runtime = new RuntimeInfo
        {
            Family = RuntimeFamily.WindowsDesktop,
            Version = "10.0",
            Architecture = TargetArchitecture.X64,
            Mode = RuntimeDeploymentMode.SmartOffline,
            AutoDetect = true,
        },
        Installer = new InstallerOptions
        {
            Scope = InstallScope.Machine,
            CreateDesktopShortcut = true,
            CreateStartMenuShortcut = true,
            LaunchAfterInstall = false,
            AllowUpgrade = true,
        },
        Output = new OutputOptions
        {
            Directory = "./dist",
            FileName = "{ProductName}_Setup_{Version}.exe",
        },
    };

    /// <summary>序列化为 JSON 字符串。</summary>
    public string Serialize(PackageProject project) => ProjectSerializer.Serialize(project);

    /// <summary>从 JSON 反序列化。</summary>
    public ProjectLoadResult Deserialize(string json) => ProjectSerializer.Deserialize(json);
}
