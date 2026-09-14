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
        SetupIconPath = @"D:\publish\RHCVP.ico",
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
        Upgrade = new UpgradeModel(true, "RHCVP"),
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
        Assert.Contains("UsePreviousAppDir=yes", script);
        Assert.Contains("OutputBaseFilename=RHCVP_Setup_1.0.0", script);
        Assert.Contains("SetupIconFile=\"D:\\publish\\RHCVP.ico\"", script);
        Assert.Contains("UninstallDisplayIcon=\"D:\\publish\\RHCVP.ico\"", script);
        Assert.Contains("[Languages]", script);
        Assert.Contains("Name: \"chinesesimp\"; MessagesFile: \"compiler:Default.isl\"", script);
        Assert.Contains("chinesesimp.WizardSelectDir=选择目标位置", script);
        Assert.Contains("WizardSmallImageFile=", script);
        Assert.Contains("WizardStyle=modern dynamic polar includetitlebar", script);
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
        Assert.Contains(@"IconFilename: ""{app}\DotSetupForge.Shortcut.ico""", script);
        Assert.Contains(@"Source: ""D:\publish\RHCVP.ico""; DestDir: ""{app}""; DestName: ""DotSetupForge.Shortcut.ico""; Flags: ignoreversion", script);
    }

    [Fact]
    public void Generate_With_WizardBrandImages_Should_Use_Images_And_Enable_WelcomePage()
    {
        var model = CreateModel() with
        {
            WizardSmallImagePath = @"D:\assets\RHCVP-small.png",
            WizardImagePath = @"D:\assets\RHCVP-welcome.png",
        };

        var script = new InnoScriptGenerator().Generate(model, Options());

        Assert.Contains("WizardSmallImageFile=\"D:\\assets\\RHCVP-small.png\"", script);
        Assert.Contains("WizardImageFile=\"D:\\assets\\RHCVP-welcome.png\"", script);
        Assert.Contains("WizardImageStretch=yes", script);
        Assert.Contains("DisableWelcomePage=no", script);
    }

    [Theory]
    [InlineData(InstallerWizardTheme.Stellar, "modern dynamic polar includetitlebar")]
    [InlineData(InstallerWizardTheme.Slate, "modern dynamic slate includetitlebar")]
    [InlineData(InstallerWizardTheme.Zircon, "modern zircon includetitlebar")]
    [InlineData(InstallerWizardTheme.ModernLight, "modern dynamic windows11 includetitlebar")]
    public void Generate_Should_Use_Selected_WizardTheme(InstallerWizardTheme theme, string expectedStyle)
    {
        var script = new InnoScriptGenerator().Generate(CreateModel() with { WizardTheme = theme }, Options());

        Assert.Contains($"WizardStyle={expectedStyle}", script);
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

    private static InstallerModel CreateModelWithRuntimePrerequisite(
        TargetArchitecture architecture = TargetArchitecture.X64) => CreateModel() with
    {
        Prerequisites =
        [
            new PrerequisiteModel(
                Id: "dotnet-windowsdesktop-10.0.1-x64",
                Name: ".NET Desktop Runtime 10.0.1 X64",
                Version: "10.0.1",
                Architecture: architecture,
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
        Assert.Contains("function Getdotnetwindowsdesktop1001x64DotNetRoot", script);
        Assert.Contains("RegQueryStringValue(HKLM32, 'SOFTWARE\\dotnet\\Setup\\InstalledVersions\\x64', 'InstallLocation', DotNetRoot)", script);
        Assert.Contains("RegQueryStringValue(HKLM64, 'SOFTWARE\\dotnet\\Setup\\InstalledVersions\\x64\\sharedhost', 'Path', DotNetRoot)", script);
        Assert.Contains("if not Result and IsWin64 then", script);
        Assert.Contains("SharedDir := AddBackslash(DotNetRoot) + 'shared\\Microsoft.WindowsDesktop.App'", script);
        Assert.DoesNotContain("{win}\\dotnet", script);
        Assert.Contains("DirExists(SharedDir)", script);
        Assert.DoesNotContain("DirectoryExists(SharedDir)", script);
        Assert.Contains("procedure InstallPrerequisitedotnetwindowsdesktop1001x64", script);
        Assert.Contains("ExtractTemporaryFile('windowsdesktop-runtime-10.0.1-win-x64.exe')", script);
        Assert.Contains("'/install /quiet /norestart'", script);
        Assert.Contains("if CurStep = ssInstall then", script);
        Assert.Contains("function NeedRestart: Boolean", script);
        Assert.Contains("DotNetRebootCode = 3010", script);
    }

    [Fact]
    public void Generate_With_RuntimePrerequisite_Should_Emit_PatchAware_SameBand_VersionDetection()
    {
        var script = new InnoScriptGenerator().Generate(CreateModelWithRuntimePrerequisite(), Options());

        Assert.Contains("function ParseVersion(S: String; var Major: Integer; var Minor: Integer; var Patch: Integer): Boolean", script);
        Assert.Contains("Patch := 0", script); // 10.0 等价于 10.0.0
        Assert.Contains("Patch := StrToIntDef", script); // 正确解析 10.0.1 的 patch
        Assert.Contains("(Major = ReqMajor) and (Minor = ReqMinor) and (Patch >= ReqPatch)", script);
        Assert.DoesNotContain("(Major > ReqMajor) or ((Major = ReqMajor) and (Minor >= ReqMinor))", script);
    }

    [Theory]
    [InlineData(TargetArchitecture.X86, "x86")]
    [InlineData(TargetArchitecture.X64, "x64")]
    [InlineData(TargetArchitecture.Arm64, "arm64")]
    public void Generate_With_RuntimePrerequisite_Should_Use_ArchitectureSpecific_RegistryKey(
        TargetArchitecture architecture,
        string registryArchitecture)
    {
        var script = new InnoScriptGenerator().Generate(
            CreateModelWithRuntimePrerequisite(architecture), Options());

        Assert.Contains($"InstalledVersions\\{registryArchitecture}', 'InstallLocation'", script);
    }

    [Fact]
    public void Generate_With_RuntimePrerequisite_Should_Emit_DontCopy_File_Entry()
    {
        var script = new InnoScriptGenerator().Generate(CreateModelWithRuntimePrerequisite(), Options());

        Assert.Contains(
            @"Source: ""D:\cache\windowsdesktop-runtime-10.0.1-win-x64.exe""; DestDir: ""{tmp}""; Flags: dontcopy",
            script);
        Assert.DoesNotContain("dontcopy  ;", script);
    }

    [Fact]
    public void Generate_Without_Prerequisite_Should_Not_Emit_Code_Section()
    {
        var script = new InnoScriptGenerator().Generate(CreateModel(), Options());

        Assert.Contains("[Code]", script);
        Assert.Contains("function GetPreviousInstallation: Boolean", script);
        Assert.Contains("function FindPreviousInstallationByDisplayName(RootKey: Integer): Boolean", script);
        Assert.Contains("RegGetSubkeyNames(RootKey, 'Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall', Keys)", script);
        Assert.Contains("CompareText(DisplayName, 'RHCVP') = 0", script);
        Assert.Contains("Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\{11111111-2222-3333-4444-555555555555}_is1", script);
        Assert.Contains("直接升级（保留用户配置和数据，推荐）", script);
        Assert.Contains("卸载后重新安装", script);
        Assert.Contains("仅卸载当前版本，不继续安装", script);
        Assert.Contains("function RunPreviousUninstaller: Boolean", script);
        Assert.Contains("function GetUninstallerParameters(UninstallString: String): String", script);
        Assert.Contains("CreateInputOptionPage(", script);
        Assert.Contains("wpSelectDir,", script);
        Assert.Contains("UninstallerParameters := GetUninstallerParameters(PreviousUninstallString);", script);
        Assert.Contains("WizardForm.Close", script);
    }

    [Fact]
    public void Generate_When_UpgradeIsDisabled_Should_Not_Emit_InstalledVersionChoice()
    {
        var model = CreateModel() with { Upgrade = new UpgradeModel(false, null) };

        var script = new InnoScriptGenerator().Generate(model, Options());

        Assert.DoesNotContain("function GetPreviousInstallation: Boolean", script);
        Assert.DoesNotContain("检测到已安装版本", script);
    }

    [Fact]
    public void Generate_With_Default_DataDriveDirectory_Should_Use_DDrive_And_Fallback_When_Missing()
    {
        var model = CreateModel() with { InstallDirectory = @"D:\Apps\测试公司\RHCVP" };

        var script = new InnoScriptGenerator().Generate(model, Options());

        Assert.Contains(@"DefaultDirName={code:GetPreferredDataDriveInstallDir|测试公司\RHCVP}", script);
        Assert.Contains("function GetPreferredDataDriveInstallDir(Param: String): String", script);
        Assert.Contains(@"if DirExists('D:\') then", script);
        Assert.Contains(@"Result := 'D:\Apps\' + Param", script);
        Assert.Contains(@"Result := ExpandConstant('{autopf}\') + Param", script);
    }

    [Fact]
    public void Generate_With_NetFrameworkRelease_Prerequisite_Should_Emit_Registry_Detection()
    {
        var model = CreateModel() with
        {
            Prerequisites =
            [
                new PrerequisiteModel(
                    "netframework-4-7", ".NET Framework 4.7", "4.7", TargetArchitecture.AnyCpu,
                    "NDP47-x86-x64-AllOS-ENU.exe", " /q /norestart", [0], [3010],
                    PrerequisiteDetection.NetFrameworkRelease, "460798", @"D:\cache\NDP47-x86-x64-AllOS-ENU.exe"),
            ],
        };

        var script = new InnoScriptGenerator().Generate(model, Options());

        Assert.Contains("RegQueryDWordValue(HKLM32, 'SOFTWARE\\Microsoft\\NET Framework Setup\\NDP\\v4\\Full', 'Release', Release)", script);
        Assert.Contains("Release >= 460798", script);
        Assert.Contains("ExtractTemporaryFile('NDP47-x86-x64-AllOS-ENU.exe')", script);
    }
}
