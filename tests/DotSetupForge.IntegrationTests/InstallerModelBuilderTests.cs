using DotSetupForge.Application.Packaging;
using DotSetupForge.Core.Analysis;
using DotSetupForge.Core.Models;
using DotSetupForge.Core.Packaging;
using Xunit;

namespace DotSetupForge.IntegrationTests;

public class InstallerModelBuilderTests
{
    private static ApplicationAnalysisResult CreateAnalysis(params ScannedFile[] files) => new()
    {
        Success = true,
        MainExecutable = @"D:\publish\FakeApp.exe",
        ApplicationName = "FakeApp",
        ApplicationType = ApplicationType.Wpf,
        TargetFramework = "net10.0",
        FrameworkName = "Microsoft.WindowsDesktop.App",
        FrameworkVersion = "10.0.0",
        RuntimeName = ".NET Desktop Runtime",
        Architecture = TargetArchitecture.X64,
        DeploymentMode = DeploymentMode.FrameworkDependent,
        Version = "1.2.3",
        Files = files,
    };

    private static ScannedFile File(string relativePath, FileCategory category) => new(
        Path.GetFileName(relativePath),
        relativePath,
        100,
        Path.GetExtension(relativePath).ToLowerInvariant(),
        "hash",
        category);

    [Fact]
    public void Build_Should_Produce_Product_And_MainExecutable()
    {
        var analysis = CreateAnalysis(File("FakeApp.exe", FileCategory.Application));
        var builder = new InstallerModelBuilder();

        var model = builder.Build(analysis);

        Assert.Equal("FakeApp", model.Product.Name);
        Assert.Equal("1.2.3", model.Product.Version);
        Assert.Equal("FakeApp.exe", model.MainExecutable);
        Assert.Equal("FakeApp.exe", model.Product.MainExecutable);
        Assert.Equal(InstallScope.Machine, model.Scope);
        Assert.Equal(@"D:\Apps\FakeApp", model.InstallDirectory);
    }

    [Fact]
    public void Build_Should_Apply_Default_FileRules()
    {
        var analysis = CreateAnalysis(
            File("FakeApp.exe", FileCategory.Application),
            File("FakeApp.pdb", FileCategory.Debug),
            File("Logs/app.log", FileCategory.Log),
            File("appsettings.json", FileCategory.Configuration));

        var model = new InstallerModelBuilder().Build(analysis);

        Assert.Equal(2, model.Files.Count);
        Assert.DoesNotContain(model.Files, f => f.SourceRelativePath.EndsWith(".pdb"));
        Assert.DoesNotContain(model.Files, f => f.SourceRelativePath.StartsWith("Logs/"));
    }

    [Fact]
    public void Build_Should_Recommend_Policies_By_Category()
    {
        var analysis = CreateAnalysis(
            File("FakeApp.exe", FileCategory.Application),
            File("appsettings.json", FileCategory.Configuration),
            File("Data/config.dat", FileCategory.Data),
            File("runtimes/win-x64/native/lib.dll", FileCategory.Native));

        var model = new InstallerModelBuilder().Build(analysis);

        var exe = model.Files.Single(f => f.SourceRelativePath == "FakeApp.exe");
        Assert.Equal(UpgradePolicy.OverwriteAlways, exe.UpgradePolicy);
        Assert.Equal(UninstallPolicy.Delete, exe.UninstallPolicy);

        var config = model.Files.Single(f => f.SourceRelativePath == "appsettings.json");
        Assert.Equal(UpgradePolicy.PreserveExisting, config.UpgradePolicy);
        Assert.Equal(UninstallPolicy.NeverUninstall, config.UninstallPolicy);

        var data = model.Files.Single(f => f.SourceRelativePath == "Data/config.dat");
        Assert.Equal(UpgradePolicy.PreserveExisting, data.UpgradePolicy);
        Assert.Equal(UninstallPolicy.NeverUninstall, data.UninstallPolicy);

        var native = model.Files.Single(f => f.SourceRelativePath.StartsWith("runtimes/"));
        Assert.Equal("runtimes/win-x64/native", native.TargetSubDirectory);
        Assert.Null(exe.TargetSubDirectory);
    }

    [Fact]
    public void Build_Should_Use_Project_Product_Overrides()
    {
        var analysis = CreateAnalysis(File("FakeApp.exe", FileCategory.Application));
        var project = new PackageProject
        {
            Product = new ProductInfo
            {
                AppId = Guid.NewGuid(),
                Name = "RHCVP",
                Version = "9.9.9",
                Publisher = "测试公司",
                MainExecutable = "FakeApp.exe",
            },
            Installer = new InstallerOptions
            {
                Scope = InstallScope.User,
                CreateDesktopShortcut = false,
                CreateStartMenuShortcut = false,
                SetupIconPath = @"D:\assets\RHCVP.ico",
                AllowUpgrade = false,
            },
        };

        var model = new InstallerModelBuilder().Build(analysis, project);

        Assert.Equal("RHCVP", model.Product.Name);
        Assert.Equal("9.9.9", model.Product.Version);
        Assert.Equal("测试公司", model.Product.Publisher);
        Assert.Equal(InstallScope.User, model.Scope);
        Assert.Empty(model.Shortcuts);
        Assert.Equal(@"D:\assets\RHCVP.ico", model.SetupIconPath);
        Assert.False(model.Upgrade.Enabled);
        Assert.Contains("RHCVP", model.InstallDirectory);
    }

    [Fact]
    public void Build_Should_Create_Shortcuts_By_Default()
    {
        var analysis = CreateAnalysis(File("FakeApp.exe", FileCategory.Application));

        var model = new InstallerModelBuilder().Build(analysis);

        Assert.Equal(2, model.Shortcuts.Count);
        Assert.Contains(model.Shortcuts, s => s.Desktop && !s.StartMenu);
        Assert.Contains(model.Shortcuts, s => s.StartMenu && !s.Desktop);
    }
}
