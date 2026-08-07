using DotSetupForge.Core.Signing;

namespace DotSetupForge.Core.Tests;

public class SigningServiceTests
{
    [Fact]
    public void BuildSignArguments_Should_Include_All_Elements()
    {
        var args = SigningService.BuildSignArguments(
            "cert.pfx", "secret", "http://ts.example.com", "Setup.exe");

        Assert.Equal(
            ["sign", "/f", "cert.pfx", "/p", "secret", "/t", "http://ts.example.com", "/fd", "SHA256", "/q", "Setup.exe"],
            args);
    }

    [Fact]
    public void BuildSignArguments_Without_Password_Should_Omit_PAnd_Value()
    {
        var args = SigningService.BuildSignArguments(
            "cert.pfx", string.Empty, null, "Setup.exe");

        Assert.Equal(["sign", "/f", "cert.pfx", "/fd", "SHA256", "/q", "Setup.exe"], args);
        Assert.DoesNotContain("/p", args);
        Assert.DoesNotContain("/t", args);
    }

    [Fact]
    public void FindOnPath_Should_Locate_Existing_Executable()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dsf-path-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var tool = Path.Combine(tempDir, "fake-tool.exe");
            File.WriteAllBytes(tool, [1, 2, 3]);

            var found = SigningService.FindOnPath("fake-tool.exe");
            Assert.Null(found); // 当前 PATH 不含临时目录

            var originalPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            Environment.SetEnvironmentVariable("PATH", tempDir + Path.PathSeparator + originalPath);
            try
            {
                var foundInPath = SigningService.FindOnPath("fake-tool.exe");
                Assert.Equal(tool, foundInPath);
            }
            finally
            {
                Environment.SetEnvironmentVariable("PATH", originalPath);
            }
        }
        finally
        {
            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch (IOException)
            {
                // 忽略
            }
        }
    }

    [Fact]
    public void FindOnPath_Missing_File_Should_Return_Null()
    {
        Assert.Null(SigningService.FindOnPath("definitely-not-exist-xyz.exe"));
    }
}
