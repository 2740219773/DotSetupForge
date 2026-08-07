# 测试资产

## RHCVP（第二阶段 ApplicationAnalyzer 集成测试）

真实发布目录（用户提供，用于 `dotpack analyze` 集成验收，无需自建 fixtures）：

```
D:\WorkProjects\2026\QXS26019 抗辐照芯片功能应用\RHCVP\QXS26019\Software\RHCVP\RHCVP\bin\Release\net10.0-windows
```

### 已确认的实际内容（2026-08 检查）

- 主程序四件套同名：`RHCVP.exe` / `RHCVP.dll` / `RHCVP.runtimeconfig.json` / `RHCVP.deps.json`
- `RHCVP.runtimeconfig.json` 使用 **`frameworks` 数组**（非单数 `framework`）：
  - `Microsoft.NETCore.App` 10.0.0
  - `Microsoft.WindowsDesktop.App` 10.0.0
  - `tfm: net10.0`
- `RHCVP.deps.json`：runtimeTarget `.NETCoreApp,Version=v10.0`，含 runtimeTargets（win-x64 等 native 资产）
- `runtimes/` 下多平台 native 文件（win-x64/linux/osx/...）
- 第三方依赖 DLL：GLWpfControl、HarfBuzzSharp、LiveChartsCore、OpenTK、SkiaSharp、System.IO.Ports
- `Data/`、`Logs/`、`appsettings.json`、`RHCVP.pdb`

### 预期分析结果

| 项 | 值 |
| --- | --- |
| 主程序 | RHCVP.exe |
| 程序类型 | WPF |
| TFM | net10.0 |
| Framework | Microsoft.WindowsDesktop.App 10.0.0 |
| Runtime | .NET Desktop Runtime |
| 架构 | x64（待 PE 分析确认） |
| 部署模式 | Framework-dependent |

## 单元测试 fixtures（tests/fixtures）

单元测试使用仓库内手写文本 fixture（runtimeconfig/deps JSON 样本），PE 样本复用本仓库构建产物与 dotnet SDK 自带可执行文件，不依赖 RHCVP。
