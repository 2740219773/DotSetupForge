using System.Text.Json;
using System.Text.Json.Serialization;
using DotSetupForge.Application.Analysis;
using DotSetupForge.Application.Build;
using DotSetupForge.Application.Projects;
using DotSetupForge.Application.Runtime;
using DotSetupForge.Core.Analysis;
using DotSetupForge.Core.Json;
using DotSetupForge.Core.Models;
using DotSetupForge.Core.Runtime;

namespace DotSetupForge.CLI;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private static int Main(string[] args)
    {
        var command = args.Length > 0 ? args[0] : string.Empty;

        return command switch
        {
            "test" => RunTest(),
            "init" => RunInit(args[1..]),
            "analyze" => RunAnalyze(args[1..]),
            "restore" => RunRestore(args[1..]),
            "build" => RunBuild(args[1..]),
            "runtime" => RunRuntime(args[1..]),
            "clean" => RunClean(args[1..]),
            "-h" or "--help" or "" => PrintUsage(),
            _ => PrintUnknown(command),
        };
    }

    private static int RunBuild(string[] args)
    {
        var input = string.Empty;
        string? outputDir = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--output-dir" when i + 1 < args.Length:
                    outputDir = args[++i];
                    break;
                default:
                    if (string.IsNullOrEmpty(input))
                    {
                        input = args[i];
                    }
                    break;
            }
        }

        if (string.IsNullOrEmpty(input))
        {
            Console.WriteLine("用法: dotpack build <目录|RHCVP.pack.json> [--output-dir <dir>]");
            return 1;
        }

        // 支持 .pack.json 或目录两种输入
        PackageProject? project = null;
        var sourceDirectory = input;

        if (input.EndsWith(".pack.json", StringComparison.OrdinalIgnoreCase))
        {
            if (!File.Exists(input))
            {
                Console.WriteLine($"错误 DP1004: 配置文件不存在 {input}");
                return 1;
            }

            var load = ProjectSerializer.Deserialize(File.ReadAllText(input));
            if (!load.Success || load.Project is null)
            {
                foreach (var error in load.Errors)
                {
                    Console.WriteLine($"错误 {error.Code}: {error.Message}");
                }
                return 1;
            }

            project = load.Project;
            sourceDirectory = Path.IsPathRooted(project.Source.Path)
                ? project.Source.Path
                : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(input))!, project.Source.Path));
        }

        Console.WriteLine($"DotSetupForge");
        Console.WriteLine($"Building {sourceDirectory}...");
        Console.WriteLine();

        var service = new BuildService();
        var result = service.BuildAsync(
            new BuildRequest(sourceDirectory, project, outputDir),
            new Progress<string>(Console.WriteLine)).GetAwaiter().GetResult();

        foreach (var warning in result.Warnings)
        {
            Console.WriteLine($"警告 {warning.Code}: {warning.Message}");
        }

        if (!result.Success)
        {
            foreach (var error in result.Errors)
            {
                Console.WriteLine($"错误 {error.Code}: {error.Message}");
            }
            Console.WriteLine();
            Console.WriteLine($"构建失败（{result.Duration.TotalSeconds:F1} 秒）");
            return 1;
        }

        Console.WriteLine();
        Console.WriteLine("构建成功");
        foreach (var artifact in result.Artifacts)
        {
            Console.WriteLine($"  {artifact}");
        }
        Console.WriteLine($"耗时 {result.Duration.TotalSeconds:F1} 秒");
        return 0;
    }

    private static int RunInit(string[] args)
    {
        var name = string.Empty;
        var version = "1.0.0";
        var publisher = string.Empty;
        var directory = string.Empty;
        var output = string.Empty;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--name" when i + 1 < args.Length: name = args[++i]; break;
                case "--version" when i + 1 < args.Length: version = args[++i]; break;
                case "--publisher" when i + 1 < args.Length: publisher = args[++i]; break;
                case "--directory" when i + 1 < args.Length: directory = args[++i]; break;
                case "--output" when i + 1 < args.Length: output = args[++i]; break;
                case "--help" or "-h":
                    Console.WriteLine("用法: dotpack init [--name <名称>] [--version <1.0.0>] [--publisher <发布者>] [--directory <发布目录>] [--output <xxx.pack.json>]");
                    return 0;
                default:
                    break;
            }
        }

        if (string.IsNullOrEmpty(name))
        {
            Console.WriteLine("错误 DP1005: 必须指定 --name（产品名称）");
            return 1;
        }

        var project = new PackageProject
        {
            SchemaVersion = PackageProject.CurrentSchemaVersion,
            Product = new ProductInfo
            {
                AppId = Guid.NewGuid(),
                Name = name,
                Version = version,
                Publisher = publisher,
            },
            Source = new SourceInfo
            {
                Type = SourceType.Directory,
                Path = string.IsNullOrEmpty(directory) ? "./publish" : directory,
                Configuration = "Release",
            },
            Runtime = new RuntimeInfo
            {
                Family = RuntimeFamily.WindowsDesktop,
                Version = string.Empty,
                Architecture = TargetArchitecture.X64,
                Mode = RuntimeDeploymentMode.SmartOffline,
                AutoDetect = true,
            },
            Installer = new InstallerOptions
            {
                Scope = InstallScope.Machine,
                CreateDesktopShortcut = true,
                CreateStartMenuShortcut = true,
                LaunchAfterInstall = false,
                AllowUpgrade = true,
            },
            Output = new OutputOptions
            {
                Directory = "./dist",
                FileName = "{ProductName}_Setup_{Version}.exe",
            },
        };

        // 指定发布目录时自动分析填充
        if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
        {
            Console.WriteLine($"分析 {directory} ...");
            var analysis = new ApplicationAnalysisService().Analyze(directory);
            if (analysis.Success)
            {
                project = project with
                {
                    Product = project.Product with
                    {
                        Name = name,
                        MainExecutable = Path.GetFileName(analysis.MainExecutable ?? string.Empty),
                        Version = string.IsNullOrEmpty(analysis.Version) ? version : analysis.Version,
                    },
                    Runtime = project.Runtime with
                    {
                        Family = analysis.FrameworkName switch
                        {
                            "Microsoft.WindowsDesktop.App" => RuntimeFamily.WindowsDesktop,
                            "Microsoft.AspNetCore.App" => RuntimeFamily.AspNetCore,
                            _ => RuntimeFamily.DotNet,
                        },
                        Version = MajorMinor(analysis.FrameworkVersion),
                        Architecture = analysis.Architecture,
                        AutoDetect = true,
                    },
                };
                Console.WriteLine($"  主程序: {Path.GetFileName(analysis.MainExecutable)}");
                Console.WriteLine($"  框架: {analysis.FrameworkName} {analysis.FrameworkVersion}");
                Console.WriteLine($"  架构: {analysis.Architecture}");
            }
            else
            {
                Console.WriteLine("  警告: 目录分析失败，已生成基础配置（可在 GUI 中补充）");
                foreach (var d in analysis.Diagnostics)
                {
                    Console.WriteLine($"    {d.Code} {d.Message}");
                }
            }
        }

        if (string.IsNullOrEmpty(output))
        {
            output = $"{name}.pack.json";
        }

        File.WriteAllText(output, new PackageProjectService().Serialize(project));
        Console.WriteLine($"已创建项目配置: {output} (AppId: {project.Product.AppId})");
        Console.WriteLine("下一步: dotpack analyze / dotpack restore / dotpack build");
        return 0;
    }

    private static string MajorMinor(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return string.Empty;
        }

        var parts = version.Split('.');
        return parts.Length >= 2 ? $"{parts[0]}.{parts[1]}" : version;
    }

    private static int RunRestore(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("用法: dotpack restore <xxx.pack.json>");
            return 1;
        }

        var input = args[0];
        if (!File.Exists(input))
        {
            Console.WriteLine($"错误 DP1004: 配置文件不存在 {input}");
            return 1;
        }

        var load = ProjectSerializer.Deserialize(File.ReadAllText(input));
        if (!load.Success || load.Project is null)
        {
            foreach (var error in load.Errors)
            {
                Console.WriteLine($"错误 {error.Code}: {error.Message}");
            }
            return 1;
        }

        var project = load.Project;
        var mode = project.Runtime.Mode;
        if (mode is not (RuntimeDeploymentMode.SmartOffline or RuntimeDeploymentMode.Online))
        {
            Console.WriteLine($"部署模式 {mode} 不需要 Runtime，无需 restore。");
            return 0;
        }

        if (string.IsNullOrEmpty(project.Runtime.Version))
        {
            Console.WriteLine("错误 DP1006: 项目未指定 Runtime 版本（可先 dotpack analyze 或编辑 .pack.json）");
            return 1;
        }

        var requirement = new RuntimeRequirement(
            project.Runtime.Family, project.Runtime.Version, project.Runtime.Architecture);
        Console.WriteLine($"解析 {requirement.Family} {requirement.Version} {requirement.Architecture} ...");

        var service = CreateRuntimeService();
        var result = service.EnsureAsync(requirement, new Progress<double>(p =>
            Console.Write($"\r下载进度: {p:P0}   "))).GetAwaiter().GetResult();

        Console.WriteLine();

        if (!result.Success)
        {
            foreach (var error in result.Errors)
            {
                Console.WriteLine($"错误 {error.Code}: {error.Message}");
            }
            return 1;
        }

        Console.WriteLine(result.FromCache
            ? $"命中缓存: {result.InstallerPath}"
            : $"已下载并缓存: {result.InstallerPath}");
        return 0;
    }

    private static int RunClean(string[] args)
    {
        var service = new RuntimeService();
        service.Clean();
        Console.WriteLine("Runtime 缓存已清理。");
        return 0;
    }

    private static int RunAnalyze(string[] args)
    {
        var directory = string.Empty;
        string? mainExe = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--main-exe" when i + 1 < args.Length:
                    mainExe = args[++i];
                    break;
                case "--help" or "-h":
                    Console.WriteLine("用法: dotpack analyze <目录> [--main-exe <exe>]");
                    return 0;
                default:
                    if (string.IsNullOrEmpty(directory))
                    {
                        directory = args[i];
                    }
                    break;
            }
        }

        if (string.IsNullOrEmpty(directory))
        {
            Console.WriteLine("用法: dotpack analyze <目录> [--main-exe <exe>]");
            return 1;
        }

        Console.WriteLine("DotSetupForge");
        Console.WriteLine("Analyzing...");
        Console.WriteLine();

        var service = new ApplicationAnalysisService();
        var result = service.Analyze(directory, mainExe);

        if (!result.Success)
        {
            foreach (var d in result.Diagnostics)
            {
                Console.WriteLine($"{d.Severity} {d.Code}: {d.Message}");
            }
            return result.Diagnostics.Any(d => d.Severity == DotSetupForge.Core.Models.DiagnosticSeverity.Warning) ? 2 : 1;
        }

        PrintAnalysis(result);

        var json = JsonSerializer.Serialize(result, JsonOptions);
        File.WriteAllText("analysis.json", json);
        Console.WriteLine();
        Console.WriteLine("Analysis successful. analysis.json generated.");

        return 0;
    }

    private static void PrintAnalysis(ApplicationAnalysisResult result)
    {
        Console.WriteLine("Application");
        Console.WriteLine($"  Name: {result.ApplicationName}");
        Console.WriteLine($"  Executable: {Path.GetFileName(result.MainExecutable)}");
        Console.WriteLine($"  Version: {result.Version}");

        Console.WriteLine();
        Console.WriteLine(".NET");
        Console.WriteLine($"  TFM: {result.TargetFramework}");
        Console.WriteLine($"  Framework: {result.FrameworkName}");
        Console.WriteLine($"  Version: {result.FrameworkVersion}");
        Console.WriteLine($"  Required Runtime: {result.RuntimeName}");

        Console.WriteLine();
        Console.WriteLine("Platform");
        Console.WriteLine($"  Architecture: {result.Architecture}");

        Console.WriteLine();
        Console.WriteLine("Deployment");
        Console.WriteLine($"  {result.DeploymentMode}");

        Console.WriteLine();
        Console.WriteLine("Files");
        Console.WriteLine($"  Total: {result.Files.Count}");

        foreach (var d in result.Diagnostics)
        {
            Console.WriteLine();
            Console.WriteLine($"{d.Severity} {d.Code}: {d.Message}");
        }
    }

    private static int RunRuntime(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("用法: dotpack runtime <list|ensure>");
            return 1;
        }

        return args[0] switch
        {
            "list" => RunRuntimeList(),
            "ensure" => RunRuntimeEnsure(args[1..]),
            _ => PrintRuntimeUsage(),
        };
    }

    private static int RunRuntimeList()
    {
        var service = new RuntimeService();
        var cached = service.ListCached();

        if (cached.Count == 0)
        {
            Console.WriteLine("缓存为空。");
            return 0;
        }

        foreach (var c in cached.OrderBy(c => c.Family).ThenBy(c => c.Version))
        {
            Console.WriteLine($"{c.Family} {c.Version} {c.Architecture}  ({c.FileName})");
        }

        return 0;
    }

    private static int RunRuntimeEnsure(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("用法: dotpack runtime ensure <desktop|dotnet|aspnetcore> <版本> [--arch x64|x86|arm64]");
            return 1;
        }

        var familyText = args[0];
        var version = args[1];
        var arch = TargetArchitecture.X64;

        for (var i = 2; i < args.Length; i++)
        {
            if (args[i] == "--arch" && i + 1 < args.Length)
            {
                arch = args[++i].ToLowerInvariant() switch
                {
                    "x86" => TargetArchitecture.X86,
                    "arm64" => TargetArchitecture.Arm64,
                    _ => TargetArchitecture.X64,
                };
            }
        }

        var family = familyText.ToLowerInvariant() switch
        {
            "dotnet" => RuntimeFamily.DotNet,
            "aspnetcore" => RuntimeFamily.AspNetCore,
            _ => RuntimeFamily.WindowsDesktop,
        };

        var requirement = new RuntimeRequirement(family, version, arch);
        var service = CreateRuntimeService();

        Console.WriteLine($"解析 {requirement.Family} {requirement.Version} {requirement.Architecture}...");

        var result = service.EnsureAsync(requirement, new Progress<double>(p =>
            Console.Write($"\r下载进度: {p:P0}   "))).GetAwaiter().GetResult();

        Console.WriteLine();

        if (!result.Success)
        {
            foreach (var error in result.Errors)
            {
                Console.WriteLine($"错误 {error.Code}: {error.Message}");
            }
            return 1;
        }

        Console.WriteLine(result.FromCache
            ? $"命中缓存: {result.InstallerPath}"
            : $"已下载并缓存: {result.InstallerPath}");
        return 0;
    }

    private static RuntimeService CreateRuntimeService()
    {
        // 离线/内网镜像：目录下按 {major.minor}/releases.json 组织
        var offlineDir = Environment.GetEnvironmentVariable("DOTSETFORGE_RELEASE_METADATA_DIR");
        if (!string.IsNullOrEmpty(offlineDir))
        {
            return new RuntimeService(
                new MicrosoftRuntimeCatalog(new FileReleaseMetadataProvider(offlineDir)));
        }

        return new RuntimeService();
    }

    private static int PrintRuntimeUsage()
    {
        Console.WriteLine("""
            用法:
              dotpack runtime list
              dotpack runtime ensure <desktop|dotnet|aspnetcore> <版本> [--arch x64|x86|arm64]

            """);
        return 1;
    }

    private static int RunTest()
    {
        Console.WriteLine("DotSetupForge");
        Console.WriteLine("Running 'test'...");
        Console.WriteLine();

        var service = new PackageProjectService();

        // 1. 创建 PackageProject
        var project = service.CreateTestProject();

        // 2. 序列化为 test.pack.json
        var json = service.Serialize(project);
        File.WriteAllText("test.pack.json", json);
        Console.WriteLine("Written: test.pack.json");

        // 3. 重新读取并验证
        var result = service.Deserialize(json);
        if (!result.Success)
        {
            foreach (var error in result.Errors)
            {
                Console.WriteLine($"FAIL: {error.Code} {error.Message}");
            }
            return 1;
        }

        var loaded = result.Project!;
        var schemaOk = loaded.SchemaVersion == DotSetupForge.Core.Models.PackageProject.CurrentSchemaVersion;
        var appIdOk = loaded.Product.AppId == project.Product.AppId;

        Console.WriteLine();
        Console.WriteLine($"  SchemaVersion : {loaded.SchemaVersion}");
        Console.WriteLine($"  AppId         : {loaded.Product.AppId}");
        Console.WriteLine($"  Name          : {loaded.Product.Name}");
        Console.WriteLine($"  Version       : {loaded.Product.Version}");

        if (schemaOk && appIdOk)
        {
            Console.WriteLine();
            Console.WriteLine("PASS: 序列化/反序列化验证通过");
            return 0;
        }

        Console.WriteLine();
        Console.WriteLine($"FAIL: schemaOk={schemaOk}, appIdOk={appIdOk}");
        return 1;
    }

    private static int PrintUsage()
    {
        Console.WriteLine("""
            DotSetupForge - .NET Windows 应用打包工具

            用法:
              dotpack test               创建 PackageProject 并验证序列化往返
              dotpack init               创建 .pack.json 项目配置
                          [--name <名称>] [--version <1.0.0>] [--publisher <发布者>]
                          [--directory <发布目录>] [--output <xxx.pack.json>]
              dotpack analyze <目录>      分析应用并生成 analysis.json
                          [--main-exe <exe>]  指定主程序（多候选时必填）
              dotpack restore <pack.json> 解析项目 Runtime 需求并确保缓存命中
              dotpack build <目录|pack.json>  完整构建安装包（需安装 Inno Setup）
                          [--output-dir <dir>]
              dotpack runtime list       列出运行时缓存
              dotpack runtime ensure <family> <版本> [--arch]  下载运行时到缓存
              dotpack clean              清理运行时缓存

            """);
        return 0;
    }

    private static int PrintUnknown(string command)
    {
        Console.WriteLine($"未知命令: {command}");
        return PrintUsage();
    }
}
