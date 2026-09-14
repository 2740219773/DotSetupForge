using DotSetupForge.Core.Runtime;

namespace DotSetupForge.Core.Tests;

public sealed class LegacyFrameworkBootstrapperCatalogTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"dsf-netfx-{Guid.NewGuid():N}");

    public LegacyFrameworkBootstrapperCatalogTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void Resolve_Uses_ProductCode_And_Release_Threshold_Not_Directory_Name()
    {
        var packageDirectory = Path.Combine(_root, "misleading-name");
        Directory.CreateDirectory(packageDirectory);
        File.WriteAllText(Path.Combine(packageDirectory, "product.xml"), """
            <Product xmlns="http://schemas.microsoft.com/developer/2004/01/bootstrapper" ProductCode=".NETFramework,Version=v4.7">
              <Commands><Command PackageFile="NDP47-x86-x64-AllOS-ENU.exe" Arguments=" /q /norestart">
                <BypassIf Property="DotNet47Full_Release" Compare="ValueGreaterThanOrEqualTo" Value="460798" />
                <ExitCodes><ExitCode Value="0" Result="Success" /><ExitCode Value="3010" Result="SuccessReboot" /></ExitCodes>
              </Command></Commands>
            </Product>
            """);

        var definition = new LegacyFrameworkBootstrapperCatalog(_root).Resolve("4.7");

        Assert.NotNull(definition);
        Assert.Equal("4.7", definition!.Version);
        Assert.Equal(460798, definition.ReleaseValue);
        Assert.Equal("NDP47-x86-x64-AllOS-ENU.exe", definition.InstallerFileName);
    }

    [Fact]
    public void Cache_Import_Copies_Source_And_Rejects_Wrong_File_Name()
    {
        var source = Path.Combine(_root, "NDP47-x86-x64-AllOS-ENU.exe");
        File.WriteAllBytes(source, [1, 2, 3]);
        var package = new LegacyFrameworkPackage("4.7", 460798, ".NET Framework 4.7", Path.GetFileName(source), " /q", [0], [3010]);
        var cache = new LegacyFrameworkCache(Path.Combine(_root, "cache"));

        var imported = cache.Import(package, source);

        Assert.True(File.Exists(source));
        Assert.True(File.Exists(imported.InstallerPath));
        Assert.NotEqual(source, imported.InstallerPath);
        var wrong = Path.Combine(_root, "wrong.exe");
        File.WriteAllBytes(wrong, [4]);
        Assert.Throws<InvalidOperationException>(() => cache.Import(package, wrong));
    }

    [Fact]
    public void Resolve_Should_Read_Release_Bypass_Inside_InstallConditions()
    {
        var packageDirectory = Path.Combine(_root, "nested-condition");
        Directory.CreateDirectory(packageDirectory);
        File.WriteAllText(Path.Combine(packageDirectory, "product.xml"), """
            <Product xmlns="http://schemas.microsoft.com/developer/2004/01/bootstrapper" ProductCode=".NETFramework,Version=v4.7">
              <Commands><Command PackageFile="NDP47-x86-x64-AllOS-ENU.exe"><InstallConditions>
                <BypassIf Property="DotNet47Full_Release" Compare="ValueGreaterThanOrEqualTo" Value="460798" />
              </InstallConditions></Command></Commands>
            </Product>
            """);

        var definition = new LegacyFrameworkBootstrapperCatalog(_root).Resolve("4.7");

        Assert.NotNull(definition);
        Assert.Equal(460798, definition!.ReleaseValue);
    }

    [Fact]
    public void ResolveForInstaller_Allows_Compatible_Higher_Framework_Package()
    {
        var packageDirectory = Path.Combine(_root, "DotNetFX472");
        Directory.CreateDirectory(packageDirectory);
        const string installer = "NDP472-KB4054530-x86-x64-AllOS-ENU.exe";
        File.WriteAllText(Path.Combine(packageDirectory, "product.xml"), $$"""
            <Product xmlns="http://schemas.microsoft.com/developer/2004/01/bootstrapper" ProductCode=".NETFramework,Version=v4.7.2">
              <Commands><Command PackageFile="{{installer}}" Arguments=" /q /norestart">
                <InstallConditions><BypassIf Property="DotNetFull_Release" Compare="ValueGreaterThan" Value="461808" /></InstallConditions>
              </Command></Commands>
            </Product>
            """);
        var selected = Path.Combine(packageDirectory, installer);
        File.WriteAllBytes(selected, [1]);

        var definition = new LegacyFrameworkBootstrapperCatalog(_root).ResolveForInstaller("4.7", selected);

        Assert.NotNull(definition);
        Assert.Equal("4.7.2", definition!.Version);
        Assert.Equal(installer, definition.InstallerFileName);
        Assert.Equal(461809, definition.ReleaseValue);
    }

    [Fact]
    public void ResolveForInstaller_And_LocalScan_Accept_Chinese_Installer_Variant()
    {
        var packageDirectory = Path.Combine(_root, "DotNetFX472");
        Directory.CreateDirectory(packageDirectory);
        const string enuInstaller = "NDP472-KB4054530-x86-x64-AllOS-ENU.exe";
        const string chsInstaller = "NDP472-KB4054530-x86-x64-AllOS-CHS.exe";
        File.WriteAllText(Path.Combine(packageDirectory, "product.xml"), $$"""
            <Product xmlns="http://schemas.microsoft.com/developer/2004/01/bootstrapper" ProductCode=".NETFramework,Version=v4.7.2">
              <Commands><Command PackageFile="{{enuInstaller}}"><InstallConditions>
                <BypassIf Property="DotNetFull_Release" Compare="ValueGreaterThan" Value="461808" />
              </InstallConditions></Command></Commands>
            </Product>
            """);
        File.WriteAllBytes(Path.Combine(packageDirectory, chsInstaller), [1]);
        var catalog = new LegacyFrameworkBootstrapperCatalog(_root);

        var fromSelection = catalog.ResolveForInstaller("4.7", Path.Combine(packageDirectory, chsInstaller));
        var fromScan = catalog.FindLocalInstallerPackage(catalog.Resolve("4.7")!);

        Assert.Equal(chsInstaller, fromSelection!.InstallerFileName);
        Assert.Equal(chsInstaller, fromScan!.InstallerFileName);
        Assert.Equal([enuInstaller, chsInstaller], LegacyFrameworkBootstrapperCatalog.GetSupportedInstallerFileNames(fromSelection));
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }
}
