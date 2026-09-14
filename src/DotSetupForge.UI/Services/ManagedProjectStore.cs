using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace DotSetupForge.UI.Services;

/// <summary>保存由 GUI 自动创建的打包项目配置。</summary>
public sealed class ManagedProjectStore
{
    /// <summary>返回工具自动保存 .pack.json 的目录，供“打开项目文件”作为默认位置。</summary>
    public string GetProjectDirectory() =>
        GetWritableDirectory(Path.Combine(AppContext.BaseDirectory, "config", "projects"))
        ?? GetWritableDirectory(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DotSetupForge", "config", "projects"))
        ?? throw new IOException("无法创建打包工具的项目配置目录");

    /// <summary>
    /// 优先使用工具旁的 config\projects；安装目录不可写时回退到当前用户的数据目录。
    /// </summary>
    public string Save(string projectName, string sourceDirectory, string content)
    {
        var directory = GetProjectDirectory();

        var safeName = string.Concat(projectName.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = "未命名项目";
        }

        var sourceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            Path.GetFullPath(sourceDirectory).ToUpperInvariant())))[..12].ToLowerInvariant();
        var path = Path.Combine(directory, $"{safeName}-{sourceHash}.pack.json");
        File.WriteAllText(path, content);
        return path;
    }

    private static string? GetWritableDirectory(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            return path;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }
}
