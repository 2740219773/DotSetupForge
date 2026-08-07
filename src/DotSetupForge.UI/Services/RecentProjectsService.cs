using System.IO;
using System.Text.Json;

namespace DotSetupForge.UI.Services;

/// <summary>最近项目记录。</summary>
public sealed record RecentProjectEntry(string Path, string Name, DateTime LastOpened);

/// <summary>最近项目持久化（%LOCALAPPDATA%\DotSetupForge\recent-projects.json）。</summary>
public sealed class RecentProjectsService
{
    private const int MaxEntries = 8;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _filePath;

    public RecentProjectsService()
    {
        var baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DotSetupForge");
        Directory.CreateDirectory(baseDir);
        _filePath = Path.Combine(baseDir, "recent-projects.json");
    }

    public IReadOnlyList<RecentProjectEntry> List()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return [];
            }

            var entries = JsonSerializer.Deserialize<List<RecentProjectEntry>>(File.ReadAllText(_filePath), JsonOptions);
            return entries?
                .Where(e => File.Exists(e.Path))
                .OrderByDescending(e => e.LastOpened)
                .Take(MaxEntries)
                .ToList() ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
        catch (IOException)
        {
            return [];
        }
    }

    public void Add(string path, string name)
    {
        var entries = List().ToList();
        entries.RemoveAll(e => string.Equals(e.Path, path, StringComparison.OrdinalIgnoreCase));
        entries.Insert(0, new RecentProjectEntry(path, name, DateTime.Now));
        Save(entries);
    }

    public void Remove(string path)
    {
        var entries = List().ToList();
        entries.RemoveAll(e => string.Equals(e.Path, path, StringComparison.OrdinalIgnoreCase));
        Save(entries);
    }

    private void Save(List<RecentProjectEntry> entries)
    {
        try
        {
            File.WriteAllText(_filePath, JsonSerializer.Serialize(entries, JsonOptions));
        }
        catch (IOException)
        {
            // 最近项目记录失败不阻塞主流程
        }
    }
}
