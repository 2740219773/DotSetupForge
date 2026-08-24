using System.Security.Cryptography;
using DotSetupForge.Core.Models;

namespace DotSetupForge.Core.Analysis;

/// <summary>目录扫描：记录文件名、相对路径、大小、扩展名、SHA256、类别。</summary>
public sealed class DirectoryScanner
{
    private static readonly string[] NativeDirs = ["runtimes", "native"];
    private static readonly HashSet<string> ConfigurationExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".config", ".ini", ".json", ".toml", ".xml", ".yaml", ".yml" };
    private static readonly HashSet<string> DataExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".db", ".db3", ".mdf", ".sqlite", ".sqlite3" };

    /// <summary>扫描目录。目录不存在时返回失败结果。</summary>
    public IReadOnlyList<ScannedFile> Scan(string directory)
    {
        var result = new List<ScannedFile>();
        if (!Directory.Exists(directory))
        {
            return result;
        }

        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            var info = new FileInfo(file);
            var relative = Path.GetRelativePath(directory, file).Replace('\\', '/');
            var extension = info.Extension.ToLowerInvariant();

            result.Add(new ScannedFile(
                info.Name,
                relative,
                info.Length,
                extension,
                ComputeSha256(file),
                Classify(relative, extension)));
        }

        return result.OrderBy(f => f.RelativePath, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static FileCategory Classify(string relative, string extension)
    {
        var parts = relative.Split('/');

        if (parts.Length > 1 && NativeDirs.Contains(parts[0], StringComparer.OrdinalIgnoreCase))
        {
            return FileCategory.Native;
        }

        if (parts[0].Equals("Data", StringComparison.OrdinalIgnoreCase))
        {
            return FileCategory.Data;
        }

        if (parts[0].Equals("Logs", StringComparison.OrdinalIgnoreCase) || extension == ".log")
        {
            return FileCategory.Log;
        }

        if (DataExtensions.Contains(extension))
        {
            return FileCategory.Data;
        }

        if (ConfigurationExtensions.Contains(extension))
        {
            return FileCategory.Configuration;
        }

        return extension switch
        {
            ".exe" => FileCategory.Application,
            ".dll" => IsRuntimeFile(relative) ? FileCategory.Runtime : FileCategory.Library,
            ".pdb" => FileCategory.Debug,
            _ => FileCategory.Unknown,
        };
    }

    private static bool IsRuntimeFile(string relative)
    {
        var name = Path.GetFileName(relative);
        return name is "hostfxr.dll" or "coreclr.dll" or "hostpolicy.dll" or "clrjit.dll"
            or "coreclrjit.dll" or "createdump.exe";
    }

    private static string ComputeSha256(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }
        catch (IOException)
        {
            return string.Empty;
        }
        catch (UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }
}
