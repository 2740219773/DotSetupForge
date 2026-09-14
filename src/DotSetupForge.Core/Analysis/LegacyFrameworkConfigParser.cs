using System.Xml.Linq;

namespace DotSetupForge.Core.Analysis;

/// <summary>读取旧式 .NET Framework 应用的 app.exe.config 启动框架声明。</summary>
public sealed class LegacyFrameworkConfigParser
{
    /// <summary>解析 supportedRuntime 的 sku（例如 .NETFramework,Version=v4.7.2）；缺失或畸形返回 null。</summary>
    public string? ParseFile(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var document = XDocument.Load(path);
            var runtime = document.Descendants("supportedRuntime").FirstOrDefault();
            var sku = runtime?.Attribute("sku")?.Value;
            if (!string.IsNullOrWhiteSpace(sku) &&
                sku.StartsWith(".NETFramework,Version=v", StringComparison.OrdinalIgnoreCase))
            {
                return sku[".NETFramework,Version=v".Length..];
            }

            var version = runtime?.Attribute("version")?.Value;
            return !string.IsNullOrWhiteSpace(version) && version.StartsWith('v')
                ? version[1..]
                : null;
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }
}
