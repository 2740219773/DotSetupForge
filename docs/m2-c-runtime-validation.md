# M2-c：.NET Runtime 真实安装验收

本文件用于在可还原的 Windows Sandbox 或虚拟机中验证 DotSetupForge 的智能离线 Runtime 安装。它只记录实际证据；未完成四组环境验证前，M2-c 不得标记为完成。

## 受控构建输入

- 应用：framework-dependent、`net10.0-windows`、x64 的 RHCVP 风格发布目录。
- 打包项目：`SmartOffline` 模式，`WindowsDesktop` Runtime，管理员安装范围。
- Runtime：当前目录缓存中已校验 SHA-256 的 `windowsdesktop-runtime-*-win-x64.exe`。
- 安装包：每次验收前必须由当前工作区代码重新生成；不得使用已有的 `dist\RHCVP_Setup_1.0.0.0.exe`。

在工作区执行（将 `<publish-dir>` 替换为实际发布目录）：

```powershell
$projectPath = "$PWD\RHCVP-M2c.pack.json"
$outputDirectory = "$PWD\dist\m2c"
dotnet run --project src/DotSetupForge.CLI -- init --name RHCVP-M2c --directory "<publish-dir>" --output $projectPath
dotnet run --project src/DotSetupForge.CLI -- restore $projectPath
dotnet run --project src/DotSetupForge.CLI -- build $projectPath --output-dir $outputDirectory
```

交付虚拟机前，保留以下证据：构建命令输出、生成的 `installer.iss`、Setup.exe 的 SHA-256 与文件大小。CLI 会在 `%TEMP%\DotSetupForge\build` 下生成 `installer.iss`；构建后将最新一份复制到 `$outputDirectory` 留档：

```powershell
$iss = Get-ChildItem "$env:TEMP\DotSetupForge\build" -Recurse -Filter installer.iss |
  Sort-Object LastWriteTime -Descending |
  Select-Object -First 1
Copy-Item $iss.FullName -Destination $outputDirectory
Get-FileHash "$outputDirectory\RHCVP-M2c_Setup_*.exe" -Algorithm SHA256
```

静态核对 `installer.iss`：

- `[Files]` 含 Runtime 安装程序且使用 `Flags: dontcopy`；
- `[Code]` 含 `Is...Installed`、`Install...`、安装后复检和 `NeedRestart`；
- Runtime 使用 `/install /quiet /norestart`；
- 版本判断仅接受相同 major/minor 且 patch 不低于要求。

## 虚拟机验收矩阵

每次测试均从干净快照开始，以管理员身份运行新生成的 Setup.exe。保存安装日志（`/LOG=<path>`）、Runtime 目录清单、应用启动结果和卸载结果。

| 环境 | 预置条件 | 预期 |
| --- | --- | --- |
| A | 未安装 .NET 10 Desktop Runtime | 自动安装 Runtime，应用安装完成并可启动。 |
| B | 已安装满足要求的 .NET 10 Desktop Runtime | 跳过 Runtime 安装，应用安装完成并可启动。 |
| C | 仅有 .NET 9 Desktop Runtime | 安装 .NET 10 Runtime，应用安装完成并可启动。 |
| D | 已安装 .NET 10 Desktop Runtime | 不重复安装 Runtime，应用安装完成并可启动。 |

## Windows 与架构兼容性

Runtime 检测不使用 `C:\\Windows`、`Program Files` 等固定路径。安装程序会先从 .NET 的 32 位注册表视图读取目标架构的 `InstallLocation`，仅在 64 位系统上再回退到 64 位视图和 `sharedhost` 路径，因此 Win10 与 Win11 的目录差异不会影响检测。

| 平台 | 目标 Runtime | 必测项 |
| --- | --- | --- |
| Windows 10 x64 | x64 | 无 Runtime 安装、已有 Runtime 跳过、安装后复检与应用启动。 |
| Windows 11 x64 | x64 | 与 Win10 x64 相同，确认 UAC 与注册表检测。 |
| Windows x86（若交付 x86 应用） | x86 | 不访问 64 位注册表视图；检测 x86 Runtime。 |
| Windows on ARM（若交付 ARM64 应用） | ARM64 | 检测 ARM64 Runtime；在真实 ARM64 设备或虚拟机执行。 |

上述平台矩阵是实机验收要求，不可由当前开发机的 x64 编译结果替代。

建议的非交互安装命令：

```powershell
Start-Process -FilePath .\RHCVP-M2c_Setup_*.exe -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/LOG=C:\Temp\DotSetupForge-M2c-setup.log' -Wait
```

## 通过标准与边界

四个环境均满足表中预期，并且安装日志没有 Runtime 安装或复检错误，才可将 M2-c 标记为完成。自动化测试和脚本静态检查只证明生成逻辑，不替代真实 Windows 环境中的 Runtime 安装证据。
