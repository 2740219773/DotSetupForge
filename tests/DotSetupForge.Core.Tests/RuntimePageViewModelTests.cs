using DotSetupForge.UI.ViewModels;

namespace DotSetupForge.Core.Tests;

public sealed class RuntimePageViewModelTests
{
    [Theory]
    [InlineData("NetFramework", "4.7", true)]
    [InlineData("DotNet", "4.7", true)]
    [InlineData("DotNet", "10.0", false)]
    [InlineData("WindowsDesktop", "8.0", false)]
    public void IsLegacyFrameworkRuntime_RecognizesStaleDotNet4xConfiguration(
        string family,
        string version,
        bool expected)
    {
        Assert.Equal(expected, RuntimePageViewModel.IsLegacyFrameworkRuntime(family, version));
    }
}
