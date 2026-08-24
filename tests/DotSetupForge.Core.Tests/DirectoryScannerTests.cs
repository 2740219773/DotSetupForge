using DotSetupForge.Core.Analysis;

namespace DotSetupForge.Core.Tests;

public sealed class DirectoryScannerTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"dsf-scanner-{Guid.NewGuid():N}");

    public DirectoryScannerTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public void Scan_Should_Classify_Common_Config_And_Data_Files_As_Preserved_Categories()
    {
        File.WriteAllText(Path.Combine(_directory, "settings.ini"), "enabled=true");
        File.WriteAllText(Path.Combine(_directory, "station.xml"), "<settings />");
        File.WriteAllText(Path.Combine(_directory, "history.sqlite"), string.Empty);
        File.WriteAllText(Path.Combine(_directory, "records.db"), string.Empty);

        var files = new DirectoryScanner().Scan(_directory);

        Assert.Equal(FileCategory.Configuration, files.Single(file => file.FileName == "settings.ini").Category);
        Assert.Equal(FileCategory.Configuration, files.Single(file => file.FileName == "station.xml").Category);
        Assert.Equal(FileCategory.Data, files.Single(file => file.FileName == "history.sqlite").Category);
        Assert.Equal(FileCategory.Data, files.Single(file => file.FileName == "records.db").Category);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
