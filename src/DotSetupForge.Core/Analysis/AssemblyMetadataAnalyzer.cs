using System.Diagnostics;
using System.Reflection;

namespace DotSetupForge.Core.Analysis;

/// <summary>程序集元数据。</summary>
public sealed record AssemblyMetadata(
    string Name,
    string AssemblyVersion,
    string FileVersion,
    string ProductVersion,
    string Company,
    string Description)
{
    public static readonly AssemblyMetadata Empty = new(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
}

/// <summary>读取程序集信息：AssemblyName / AssemblyVersion / FileVersion / ProductVersion / Company / Description。</summary>
public sealed class AssemblyMetadataAnalyzer
{
    public AssemblyMetadata Analyze(string filePath)
    {
        var (name, assemblyVersion) = ReadAssemblyName(filePath);
        var fvi = FileVersionInfo.GetVersionInfo(filePath);

        return new AssemblyMetadata(
            name,
            assemblyVersion,
            fvi.FileVersion ?? string.Empty,
            fvi.ProductVersion ?? string.Empty,
            fvi.CompanyName ?? string.Empty,
            fvi.FileDescription ?? string.Empty);
    }

    private static (string Name, string Version) ReadAssemblyName(string filePath)
    {
        try
        {
            var asm = AssemblyName.GetAssemblyName(filePath);
            return (asm.Name ?? string.Empty, asm.Version?.ToString() ?? string.Empty);
        }
        catch (BadImageFormatException)
        {
            return (string.Empty, string.Empty);
        }
        catch (IOException)
        {
            return (string.Empty, string.Empty);
        }
    }
}
