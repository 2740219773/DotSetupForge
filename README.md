# DotSetupForge

DotSetupForge 是一个面向 .NET Windows 应用的智能打包与部署工具。

## 项目定位

DotSetupForge 实现 .NET 应用分析、Runtime 识别、依赖管理、安装模型生成和 Windows 安装包自动构建。

## 第一阶段目标

```text
应用发布目录
 ↓
ApplicationAnalyzer
 ↓
RuntimeResolver
 ↓
InstallerModel
 ↓
Inno Setup
 ↓
Setup.exe
```

## 技术方向

- C# / .NET 10
- WPF
- Inno Setup
- CLI 自动化

## 当前状态

项目初始化阶段。

第一个目标：实现 .NET 应用分析器。