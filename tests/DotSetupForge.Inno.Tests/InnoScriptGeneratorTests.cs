using DotSetupForge.Core.Analysis;
using DotSetupForge.Core.Models;
using DotSetupForge.Core.Packaging;
using DotSetupForge.Inno;
using Xunit;

namespace DotSetupForge.Inno.Tests;

public class InnoScriptGeneratorTests
{
    private static InstallerModel CreateModel() => new()
    {
        Product = new ProductModel(
            Guid.Parse("11111111-2222-3333-4444-555555555555"),
            "RHCVP",
            "1.0.0",
            "测试公司",
            "RHCVP.exe"),
        Scope = InstallScope.Machine,
        InstallDirectory = @"{autopf}\测试公司\RHCVP",
        MainExecutable = "RHCVP.exe",
        Files =
        [
            new InstallerFile(
                @"D:\publish\RHCVP.exe", "RHCVP.exe", 100,
                FileCategory.Application, InstallLocation.ApplicationDirectory,
                null, UpgradePolicy.OverwriteAlways, UninstallPolicy.Delete),
            new InstallerFile(
                @"D:\publish\appsettings.json", "appsettings.json", 100,
                FileCategory.Configuration, InstallLocation.ApplicationDirectory,
                null, UpgradePolicy.PreserveExisting, UninstallPolicy.NeverUninstall),
            new InstallerFile(
                @"D:\publish\runtimes\win-x64\native\lib.dll", "runtimes/win-x64/native/lib.dll", 100,
                FileCategory.Native, InstallLocation.ApplicationDirectory,
                "runtimes/win-x64/native", UpgradePolicy.OverwriteAlways, UninstallPolicy.Delete),
        ],
        Shortcuts =
        [
            new ShortcutModel("RHCVP", "RHCVP.exe", null, Desktop: true, StartMenu: false),
        ],
        LaunchAfterInstall = true,
    };

    private static InnoScriptOptions Options() => new("RHCVP_Setup_1.0.0", @".\dist");

    [Fact]
    public void Generate_Should_Contain_Setup_Section()
    {
        var script = new InnoScriptGenerator().Generate(CreateModel(), Options());

        Assert.Contains("[Setup]", script);
        Assert.Contains("AppId={{11111111-2222-3333-4444-555555555555}}", script);
        Assert.Contains("AppName=RHCVP", script);
        Assert.Contains("AppVersion=1.0.0", script);
        Assert.Contains("AppPublisher=测试公司", script);
        Assert.Contains(@"DefaultDirName={autopf}\测试公司\RHCVP", script);
        Assert.Contains("OutputBaseFilename=RHCVP_Setup_1.0.0", script);
    }

    [Fact]
    public void Generate_Should_Emit_Files_With_Policy_Flags()
    {
        var script = new InnoScriptGenerator().Generate(CreateModel(), Options());

        // 普通文件：ignoreversion
        Assert.Contains(
            @"Source: ""D:\publish\RHCVP.exe""; DestDir: ""{app}""; Flags: ignoreversion",
            script);

        // 配置：onlyifdoesntexist + uninsneveruninstall（升级保留、卸载保留）
        Assert.Contains(
            @"Source: ""D:\publish\appsettings.json""; DestDir: ""{app}""; Flags: ignoreversion onlyifdoesntexist uninsneveruninstall",
            script);

        // native：保留子目录
        Assert.Contains(
            @"DestDir: ""{app}\runtimes\win-x64\native""",
            script);
    }

    [Fact]
    public void Generate_Should_Emit_Icons()
    {
        var script = new InnoScriptGenerator().Generate(CreateModel(), Options());

        Assert.Contains("[Icons]", script);
        Assert.Contains(@"Name: ""{autodesktop}\RHCVP""; Filename: ""{app}\RHCVP.exe""", script);
    }

    [Fact]
    public void Generate_Should_Emit_Run_When_LaunchAfterInstall()
    {
        var script = new InnoScriptGenerator().Generate(CreateModel(), Options());

        Assert.Contains("[Run]", script);
        Assert.Contains(@"Filename: ""{app}\RHCVP.exe""; Description: ""运行 RHCVP""", script);
    }

    [Fact]
    public void Generate_Should_Not_Emit_Run_When_Disabled()
    {
        var model = CreateModel() with { LaunchAfterInstall = false };
        var script = new InnoScriptGenerator().Generate(model, Options());

        Assert.DoesNotContain("[Run]", script);
    }

    [Fact]
    public void Generate_To_File_Should_Write_InstallerIss()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"dsf-iss-{Guid.NewGuid():N}");
        try
        {
            var result = new InnoScriptGenerator().GenerateToFile(CreateModel(), Options(), dir);

            Assert.True(File.Exists(result.IssPath));
            Assert.Equal("installer.iss", Path.GetFileName(result.IssPath));
            Assert.Equal(result.Script, File.ReadAllText(result.IssPath));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static InstallerModel CreateModelWithRuntimePrerequisite() => CreateModel() with
    {
        Prerequisites =
        [
            new PrerequisiteModel(
                Id: "dotnet-windowsdesktop-10.0.1-x64",
                Name: ".NET Desktop Runtime 10.0.1 X64",
                Version: "10.0.1",
                Architecture: TargetArchitecture.X64,
                InstallerFileName: "windowsdesktop-runtime-10.0.1-win-x64.exe",
                InstallArguments: "/install /quiet /norestart",
                SuccessExitCodes: [0],
                RebootExitCodes: [3010],
                Detection: PrerequisiteDetection.FrameworkDirectory,
                DetectionPath: "Microsoft.WindowsDesktop.App",
                SourcePath: @"D:\cache\windowsdesktop-runtime-10.0.1-win-x64.exe"),
        ],
    };

    [Fact]
    public void Generate_With_RuntimePrerequisite_Should_Emit_Code_Section()
    {
        var script = new InnoScriptGenerator().Generate(CreateModelWithRuntimePrerequisite(), Options());

        Assert.Contains("[Code]", script);
        Assert.Contains("function Isdotnetwindowsdesktop1001x64Installed: Boolean", script);
        Assert.Contains("ExpandConstant('{win}\\dotnet\\shared\\Microsoft.WindowsDesktop.App')", script);
        Assert.Contains("procedure InstallPrerequisitedotnetwindowsdesktop1001x64", script);
        Assert.Contains("'/install /quiet /norestart'", script);
        Assert.Contains("if CurStep = ssInstall then", script);
        Assert.Contains("function NeedRestart: Boolean", script);
        Assert.Contains("DotNetRebootCode = 3010", script);
    }

    [Fact]
    public void Generate_With_RuntimePrerequisite_Should_Emit_DontCopy_File_Entry()
    {
        var script = new InnoScriptGenerator().Generate(CreateModelWithRuntimePrerequisite(), Options());

        Assert.Contains(
            @"Source: ""D:\cache\windowsdesktop-runtime-10.0.1-win-x64.exe""; DestDir: ""{tmp}""; Flags: dontcopy",
            script);
    }

    [Fact]
    public void Generate_Without_Prerequisite_Should_Not_Emit_Code_Section()
    {
        var script = new InnoScriptGenerator().Generate(CreateModel(), Options());

        Assert.DoesNotContain("[Code]", script);
    }
}
