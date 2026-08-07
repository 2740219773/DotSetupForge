# DotSetupForge 架构

> 待完善：本文件随开发推进逐步补充。

## 分层

```text
DotSetupForge.UI (WPF)
        ↓
DotSetupForge.Application (项目管理 / 工作流 / Build Job)
        ↓
DotSetupForge.Core (分析 / Runtime / 文件规则 / 模型)
        ↓
DotSetupForge.Inno (Inno Setup 脚本生成与编译)
```

GUI 与 CLI 调用完全相同的核心逻辑；`InstallerModel` 是安装器生成模块与业务分析层之间的隔离边界。

## 核心模块（规划）

- ProjectManager — 打包项目管理
- ApplicationAnalyzer — .NET 应用分析
- FileRuleEngine — 安装文件管理
- RuntimeResolver / RuntimeCatalog — .NET Runtime 分析、获取、缓存
- PrerequisiteManager — 前置组件管理
- InstallerGenerator / InstallerCompiler — Inno Setup 脚本生成与编译
- SigningService — 数字签名
- BuildPipeline — 完整构建流程
- CLI — 自动化打包
