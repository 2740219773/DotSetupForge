# 通用 .NET Windows 应用打包工具——软件设计方案 V1.0

这个工具不应只定位成“把一个文件夹压成 Setup.exe”，而应该定位成：

> 面向 C#/.NET Windows 桌面程序的智能发布、依赖分析、环境部署和安装包生成工具。

它负责理解 `.NET` 应用需要什么运行环境、应该安装哪些依赖、哪些文件应该进入安装包、升级时哪些文件应该保留，然后调用成熟的安装引擎生成最终 `Setup.exe`。

下面这套方案可以直接作为后续开发的总体设计文档。

------

## 一、项目定位

暂定项目名称：

**DotNetPackager**

后续可以重新起一个更正式的名字。

主要解决现在这种场景：

```text
RHCVP
├─ Data
├─ Logs
├─ runtimes
├─ appsettings.json
├─ GLWpfControl.dll
├─ HarfBuzzSharp.dll
├─ LiveChartsCore.dll
├─ ...
├─ RHCVP.dll
├─ RHCVP.exe
├─ RHCVP.deps.json
├─ RHCVP.runtimeconfig.json
└─ System.IO.Ports.dll
```

传统操作往往是：

```text
编译程序
→ 找输出目录
→ 配置 Inno Setup
→ 判断 .NET 版本
→ 下载 Runtime
→ 修改安装脚本
→ 添加快捷方式
→ 设置版本号
→ 生成 Setup
→ 签名
```

DotNetPackager 将其变成：

```text
选择项目/程序目录
        ↓
自动分析
        ↓
.NET 10
WPF
x64
Microsoft.WindowsDesktop.App
        ↓
选择打包策略
        ↓
生成
        ↓
RHCVP_Setup_1.0.0.exe
```

------

# 二、核心设计原则

整个系统建议遵循六个原则。

### 1. 不自己开发 Windows 安装引擎

底层使用：

**Inno Setup**

我们只负责：

- 分析
- 配置
- 依赖
- Runtime
- 脚本生成
- 编译调用
- 签名
- 验证

安装、卸载、快捷方式、注册表、UAC、文件复制等成熟功能交给 Inno Setup。

------

### 2. GUI 和核心逻辑分离

不能写成：

```text
MainWindow.xaml.cs
里面几千行代码
```

应该：

```text
GUI
 ↓
Application
 ↓
Core
 ↓
Installer Engine
```

以后 CLI、CI/CD 都直接复用 Core。

------

### 3. 所有配置均可保存

第一次 GUI 配置：

```text
RHCVP.pack.json
```

以后：

```text
dotpack build RHCVP.pack.json
```

即可自动构建。

------

### 4. 支持“程序目录”和“源码项目”两种输入

这是很重要的一点。

#### 模式 A：目录打包

选择：

```text
bin\Release\net10.0-windows
```

或者更推荐：

```text
bin\Release\net10.0-windows\publish
```

工具分析已有程序。

适合现有项目。

#### 模式 B：项目打包

直接选择：

```text
RHCVP.csproj
```

工具自己执行：

```text
dotnet publish
```

然后再制作安装包。

长期来看模式 B 更完善。

------

### 5. 依赖系统插件化

不能把代码写死成：

```text
if (.NET10)
if (WebView2)
if (VC++)
```

而应该抽象成：

```text
Prerequisite
```

即“安装前置依赖”。

以后可以支持：

```text
.NET Runtime
.NET Desktop Runtime
ASP.NET Core Runtime
VC++ Runtime
WebView2
NI-VISA
数据库
驱动
厂商 Runtime
自定义 EXE
自定义 MSI
```

这对以后打包上位机程序很重要。

------

### 6. 离线环境优先

很多工业现场电脑：

- 没互联网
- 网络受限
- 内网
- 无法临时下载安装环境

因此默认推荐：

> Runtime 一并嵌入 Setup.exe。

而不是安装时在线下载。

------

# 三、总体技术选型

建议使用：

| 模块     | 技术                                 |
| -------- | ------------------------------------ |
| 主程序   | C#                                   |
| 平台     | .NET 10                              |
| GUI      | WPF                                  |
| MVVM     | CommunityToolkit.Mvvm                |
| 配置     | JSON                                 |
| JSON     | System.Text.Json                     |
| 模板生成 | Scriban                              |
| 安装引擎 | Inno Setup                           |
| CLI      | C# Console                           |
| 日志     | Serilog                              |
| HTTP     | HttpClient                           |
| PE 分析  | System.Reflection.PortableExecutable |
| 哈希     | SHA256/SHA512                        |
| 数字签名 | signtool                             |
| 单元测试 | xUnit                                |
| 自身部署 | Self-contained                       |

DotNetPackager 自己建议发布成：

```text
win-x64
Self-contained
```

这样运行打包工具的电脑不需要提前安装 .NET。

------

# 四、总体架构

建议采用：

```text
┌─────────────────────────────────┐
│        DotNetPackager.UI        │
│             WPF                 │
└───────────────┬─────────────────┘
                │
┌───────────────▼─────────────────┐
│    DotNetPackager.Application   │
│                                 │
│ 项目管理 / 工作流 / Build Job   │
└───────────────┬─────────────────┘
                │
┌───────────────▼──────────────────────────┐
│              Packager.Core              │
│                                          │
│ ProjectAnalyzer                          │
│ PublishEngine                            │
│ RuntimeResolver                          │
│ PrerequisiteManager                     │
│ PackageManifest                          │
│ FileRuleEngine                           │
│ SigningService                           │
│ VersionService                           │
└───────┬─────────────────────┬────────────┘
        │                     │
        ▼                     ▼
┌───────────────┐      ┌──────────────────┐
│ Installer.Inno│      │ Runtime Providers │
│               │      │                   │
│ ISS Generator │      │ Microsoft .NET    │
│ ISCC Compiler │      │ VC++ / WebView2   │
└───────┬───────┘      └──────────────────┘
        │
        ▼
┌──────────────────────────────┐
│      XXX_Setup.exe           │
└──────────────────────────────┘
```

另外一个入口：

```text
DotNetPackager.CLI
        │
        └────→ Application
```

GUI 和 CLI 调用完全相同的核心逻辑。

------

# 五、建议的解决方案结构

```text
DotNetPackager.sln

src
├─ DotNetPackager.UI
│  ├─ Views
│  ├─ ViewModels
│  ├─ Controls
│  └─ Services
│
├─ DotNetPackager.Application
│  ├─ Build
│  ├─ Projects
│  ├─ Commands
│  └─ Workflows
│
├─ DotNetPackager.Core
│  ├─ Analysis
│  ├─ Runtime
│  ├─ Prerequisites
│  ├─ Publishing
│  ├─ Packaging
│  ├─ Signing
│  ├─ Versions
│  ├─ Files
│  └─ Models
│
├─ DotNetPackager.Inno
│  ├─ Compiler
│  ├─ Generator
│  ├─ Templates
│  └─ Scripts
│
└─ DotNetPackager.CLI

tests
├─ DotNetPackager.Core.Tests
├─ DotNetPackager.Inno.Tests
└─ DotNetPackager.IntegrationTests

tools
└─ inno

templates
└─ default
```

------

# 六、完整业务流程

用户第一次创建安装项目：

```text
启动 DotNetPackager
        ↓
新建打包项目
        ↓
选择 .csproj 或程序目录
        ↓
应用分析
        ↓
识别主程序
        ↓
识别 .NET Framework
        ↓
识别架构
        ↓
分析文件
        ↓
生成推荐配置
        ↓
用户调整
        ↓
检查 Runtime 缓存
        ↓
缺失则下载
        ↓
生成 Inno Script
        ↓
ISCC 编译
        ↓
数字签名
        ↓
验证
        ↓
Setup.exe
```

后续再次构建：

```text
RHCVP.pack.json
        ↓
dotpack build
        ↓
自动完成全部流程
```

------

# 七、项目输入设计

首页提供两种创建方式：

```text
新建打包项目

[ 从 C# 项目创建 ]

选择 .csproj / .sln
自动执行 dotnet publish


[ 从现有程序创建 ]

选择 exe 或发布目录
自动分析程序集
```

建议优先提示用户使用：

```text
publish
```

而不是普通：

```text
bin\Release
```

因为 `publish` 才是真正面向部署的输出。

------

# 八、应用分析器设计

核心类：

```text
ApplicationAnalyzer
```

输入：

```text
D:\RHCVP\bin\Release\net10.0-windows\
```

输出：

```text
ApplicationAnalysisResult
```

内容：

```text
主程序
程序名称
程序集名称
程序集版本
文件版本
.NET TFM
Framework
Runtime 类型
CPU 架构
是否 Self-contained
是否 Single-file
依赖 DLL
原生 DLL
配置文件
资源文件
可能的 Runtime
```

------

# 九、主程序识别

首先扫描：

```text
*.exe
```

优先寻找：

```text
XXX.exe
XXX.dll
XXX.runtimeconfig.json
XXX.deps.json
```

四者同名。

例如：

```text
RHCVP.exe
RHCVP.dll
RHCVP.runtimeconfig.json
RHCVP.deps.json
```

即可高度确定：

```text
RHCVP.exe
```

是主程序。

如果发现：

```text
AAA.exe
BBB.exe
CCC.exe
```

则 GUI 提示用户选择。

------

# 十、.NET Runtime 自动分析

这是整个软件最核心的功能之一。

读取：

```text
RHCVP.runtimeconfig.json
```

例如：

```json
{
  "runtimeOptions": {
    "tfm": "net10.0",
    "framework": {
      "name": "Microsoft.WindowsDesktop.App",
      "version": "10.0.0"
    }
  }
}
```

分析为：

```text
TFM
net10.0

.NET
10

Shared Framework
Microsoft.WindowsDesktop.App

Runtime 类型
.NET Desktop Runtime

最低需求
10.0
```

------

# 十一、Runtime 映射规则

建立统一映射表：

```text
Microsoft.NETCore.App
        ↓
.NET Runtime


Microsoft.WindowsDesktop.App
        ↓
.NET Desktop Runtime


Microsoft.AspNetCore.App
        ↓
ASP.NET Core Runtime
```

如果检测到：

```text
Microsoft.WindowsDesktop.App
```

则不再额外打包：

```text
Microsoft.NETCore.App
```

因为 Desktop Runtime 已经包含基础 Runtime。

------

# 十二、架构检测

支持：

```text
x86
x64
arm64
AnyCPU
```

分析来源可以包括：

1. PE Header
2. deps.json
3. runtimeconfig
4. `.csproj`
5. RuntimeIdentifier
6. PlatformTarget

输出：

```text
运行架构：x64
置信度：高
```

如果无法确定：

```text
检测结果：

AnyCPU

请选择目标环境：

● x64
○ x86
○ arm64
```

不要强行猜。

------

# 十三、.NET 部署模式

建议设计四种模式。

### 模式一：智能离线部署

默认推荐。

```text
Setup.exe
├─ 应用程序
└─ .NET Runtime
```

目标机：

```text
检查 Runtime

有
↓
跳过

没有
↓
静默安装
```

这是主要模式。

------

### 模式二：智能在线部署

安装包不包含 Runtime。

安装时：

```text
缺少 Runtime
↓
Microsoft 下载
↓
安装
```

优点：

安装包小。

缺点：

依赖网络。

工业项目不建议作为默认方式。

------

### 模式三：Self-contained

如果输入为 `.csproj`，DotNetPackager 可以直接执行：

```text
dotnet publish
```

生成自包含程序。

例如：

```text
win-x64
SelfContained=true
```

此时不安装系统 Runtime。

适合：

- 不希望改系统环境
- 客户环境复杂
- 没有管理员权限

------

### 模式四：不处理 Runtime

用于客户电脑已经统一部署环境的情况。

```text
○ 不包含 .NET Runtime
```

------

# 十四、Runtime 下载与缓存系统

设计：

```text
RuntimeCatalogService
```

负责：

```text
获取 Runtime 信息
解析版本
下载
校验
缓存
```

本地缓存：

```text
%LOCALAPPDATA%
└─ DotNetPackager
   └─ Cache
      └─ DotNet
         ├─ 8.0
         │  └─ windowsdesktop-runtime-8.0.xx-win-x64.exe
         │
         ├─ 9.0
         │
         └─ ...
         │
         └─ 10.0
            └─ windowsdesktop-runtime-10.0.xx-win-x64.exe
```

第一次：

```text
下载 90 MB
```

第二次打包另外一个 .NET 10 软件：

```text
直接复用
```

不重复下载。

------

# 十五、Runtime 安装检测

目标机器启动 Setup 后：

```text
判断目标架构
        ↓
定位 dotnet 安装目录
        ↓
检查 shared framework
        ↓
Microsoft.WindowsDesktop.App
        ↓
判断兼容版本
```

例如应用要求：

```text
Microsoft.WindowsDesktop.App 10.0
```

电脑存在：

```text
10.0.1
```

则：

```text
满足
```

存在：

```text
9.0.15
```

则：

```text
不满足
```

默认兼容策略建议保守：

> 同 Major.Minor，Patch 允许向上兼容。

高级设置以后可以开放 Roll Forward 策略。

------

# 十六、前置依赖系统

这是建议从第一版架构上就留好的能力。

统一接口：

```csharp
IPrerequisiteProvider
```

概念模型：

```text
Prerequisite

ID
名称
版本
架构
检测规则
安装源
安装命令
静默参数
成功返回码
重启返回码
管理员权限
依赖顺序
```

例如：

```text
dotnet-desktop-10-x64
```

配置：

```text
名称：
.NET 10 Desktop Runtime

Detection：
FrameworkDirectory

Install：
windowsdesktop-runtime-10.xxx-win-x64.exe

Arguments：
/install /quiet /norestart

Success:
0

RebootRequired:
3010
```

------

# 十七、自定义依赖

高级用户可以添加：

```text
添加前置组件
```

例如：

```text
NI-VISA Runtime
```

配置：

```text
组件名称：
NI-VISA

安装文件：
visa-runtime.exe

检测方式：
注册表

检测路径：
HKLM\...

静默参数：
/quiet

成功码：
0
```

以后就能真正用于工业软件。

------

# 十八、文件规则系统

不能简单粗暴：

```text
整个 Release 目录全部打包
```

需要：

```text
FileRuleEngine
```

默认识别：

```text
*.exe
*.dll
*.json
*.config
runtimes/**
Data/**
资源文件
```

默认建议忽略：

```text
*.pdb
*.xml 调试文档
Logs/**
*.log
obj/**
临时文件
```

GUI 提供：

```text
应用文件

☑ RHCVP.exe
☑ RHCVP.dll
☑ appsettings.json
☑ Data
☑ runtimes
☐ Logs
☐ RHCVP.pdb
```

------

# 十九、Data、Logs 和配置目录必须特殊设计

从你截图里已经能看到：

```text
Data
Logs
appsettings.json
```

这里必须提前解决一个非常典型的问题：

如果程序安装在：

```text
C:\Program Files\RHCVP
```

普通用户默认不应该随意向 Program Files 写日志。

所以建议文件规则支持：

```text
文件类型
```

例如：

### 程序文件

```text
RHCVP.exe
*.dll
```

安装：

```text
{app}
```

------

### 静态数据

例如：

```text
Data\
```

可以配置：

```text
{app}\Data
```

或者：

```text
{commonappdata}\RHCVP\Data
```

------

### 日志

推荐：

```text
%ProgramData%\RHCVP\Logs
```

或者：

```text
%LocalAppData%\RHCVP\Logs
```

而不是：

```text
Program Files\RHCVP\Logs
```

------

# 二十、配置文件升级策略

例如：

```text
appsettings.json
```

需要支持：

```text
○ 每次升级覆盖
● 首次安装创建，以后保留
● 卸载时保留
```

否则软件从：

```text
V1.0 → V1.1
```

可能把客户现场配置全部覆盖掉。

这是工业软件打包尤其需要关注的地方。

`Data/**` 中的业务数据采用同一策略：首次安装可写入发布包提供的初始数据，之后升级仅补齐缺失文件，卸载时保留。这样不会覆盖现场数据库、历史记录或用户维护的数据文件。

------

# 二十一、安装行为设置

GUI 提供：

```text
安装设置
```

内容：

```text
安装范围

● 所有用户
○ 当前用户
```

默认路径：

```text
C:\Program Files\Publisher\Product
```

其他选项：

```text
☑ 创建桌面快捷方式
☑ 创建开始菜单快捷方式
☑ 注册“已安装的应用”
☑ 安装完成后允许运行程序
☑ 支持覆盖升级
☑ 安装日志
☑ 保留用户配置
```

------

# 二十二、权限策略

建议支持两种。

### Per-machine

安装到：

```text
Program Files
```

需要：

```text
管理员权限
```

同时能够安装系统级 Runtime。

默认选择这个模式。

------

### Per-user

安装到：

```text
%LocalAppData%\Programs\Product
```

通常不需要管理员权限。

但如果还需要安装系统 `.NET Runtime`，仍可能需要提权。

所以软件应该自动提示：

```text
当前部署方式需要安装系统 .NET Runtime，
因此安装过程需要管理员权限。

如果希望无需管理员权限，可考虑 Self-contained。
```

------

# 二十三、版本管理

读取：

```text
AssemblyVersion
FileVersion
Package Version
```

优先级可设置。

GUI：

```text
程序版本

自动检测：1.2.4

安装包版本：
1.2.4

输出文件：
RHCVP_Setup_1.2.4.exe
```

建议内部统一使用：

```text
SemVer
```

例如：

```text
1.0.0
1.1.0
2.0.0
```

------

# 二十四、AppId

每个打包项目首次创建时生成：

```text
GUID
```

例如：

```text
{FA6D8EC2-....}
```

作为：

```text
AppId
```

以后：

```text
RHCVP 1.0
RHCVP 1.1
RHCVP 1.2
```

始终使用同一个 AppId。

否则 Windows 会认为是三个不同的软件。

因此：

> AppId 必须写进 `.pack.json`，生成以后禁止自动改变。

------

# 二十五、升级机制

第一阶段实现：

```text
覆盖升级
```

流程：

```text
Setup 1.2
       ↓
发现已经存在 RHCVP 1.1
       ↓
保留配置
       ↓
停止相关进程
       ↓
更新程序文件
       ↓
完成
```

需要处理：

```text
程序正在运行
```

提示：

```text
RHCVP 正在运行。

[关闭并继续]
[取消]
```

后期再考虑真正的：

```text
自动更新
```

可参考 Velopack。

第一版不建议一起做。

------

# 二十六、卸载机制

支持：

```text
Windows 设置
→ 应用
→ RHCVP
→ 卸载
```

默认删除：

```text
程序文件
快捷方式
安装记录
```

可配置：

```text
用户配置
数据
日志
```

例如：

```text
卸载 RHCVP

☐ 删除用户配置
☐ 删除运行日志
☐ 删除数据
```

工业软件默认最好：

> 不删除用户数据。

------

# 二十七、Inno Setup 集成架构

不要让业务层到处拼：

```text
"[Setup]\nAppName=" + name
```

建议：

```text
InstallerModel
        ↓
Scriban Template
        ↓
Generated.iss
```

模板结构：

```text
Templates
│
├─ Setup.sbn
├─ Files.sbn
├─ Icons.sbn
├─ Registry.sbn
├─ Run.sbn
│
└─ Code
   ├─ DotNetDetection.iss
   ├─ Prerequisite.iss
   ├─ ProcessDetection.iss
   └─ Upgrade.iss
```

最终自动生成：

```text
Generated
└─ RHCVP
   ├─ installer.iss
   ├─ runtime
   └─ package
```

然后：

```text
ISCC.exe installer.iss
```

生成：

```text
RHCVP_Setup_1.0.0.exe
```

------

# 二十八、数字签名

专业软件必须预留。

设置：

```text
代码签名

☐ 启用

证书：
XXXXXXXX

时间戳服务器：
XXXXXXXX
```

构建流程：

```text
程序文件
↓
可选：签名 EXE/DLL
↓
生成 Setup
↓
签名 Setup.exe
↓
验证签名
```

以后如果公司采购代码签名证书可以直接使用。

------

# 二十九、项目配置文件

推荐：

```text
RHCVP.pack.json
```

例如：

```json
{
  "schemaVersion": 1,

  "product": {
    "id": "FA6D8EC2-XXXX-XXXX-XXXX-XXXXXXXXXXXX",
    "name": "RHCVP",
    "publisher": "XXX科技有限公司",
    "version": "1.0.0",
    "executable": "RHCVP.exe"
  },

  "source": {
    "type": "directory",
    "path": "./bin/Release/net10.0-windows",
    "configuration": "Release"
  },

  "runtime": {
    "mode": "offline",
    "framework": "Microsoft.WindowsDesktop.App",
    "version": "10.0",
    "architecture": "x64",
    "autoDetect": true
  },

  "installer": {
    "scope": "machine",
    "createDesktopShortcut": true,
    "createStartMenuShortcut": true,
    "allowUpgrade": true,
    "launchAfterInstall": true
  },

  "files": {
    "exclude": [
      "*.pdb",
      "Logs/**"
    ]
  },

  "signing": {
    "enabled": false
  },

  "output": {
    "directory": "./dist",
    "fileName": "{ProductName}_Setup_{Version}.exe"
  }
}
```

这样所有配置都能：

```text
Git 管理
```

------

# 三十、GUI 设计

建议不要搞传统“属性表 + 一堆按钮”。

采用左侧步骤式导航。

```text
┌───────────────────────────────────────────────────────────┐
│ DotNetPackager                            RHCVP.pack.json │
├───────────────┬───────────────────────────────────────────┤
│               │                                           │
│  ① 应用       │          当前配置页面                     │
│               │                                           │
│  ② 文件       │                                           │
│               │                                           │
│  ③ 运行环境   │                                           │
│               │                                           │
│  ④ 安装设置   │                                           │
│               │                                           │
│  ⑤ 版本       │                                           │
│               │                                           │
│  ⑥ 签名       │                                           │
│               │                                           │
│  ⑦ 构建       │                                           │
│               │                                           │
├───────────────┴───────────────────────────────────────────┤
│                                      [保存] [生成安装包] │
└───────────────────────────────────────────────────────────┘
```

------

# 三十一、应用页面

例如导入 RHCVP：

```text
应用程序

源目录
D:\...\RHCVP\bin\Release\net10.0-windows
                                            [重新选择]

主程序
RHCVP.exe

程序名称
RHCVP

版本
1.0.0

──────────────────────────────────

自动分析结果

程序类型          WPF
.NET              .NET 10
Framework         Microsoft.WindowsDesktop.App
架构              x64
部署类型          Framework-dependent

✓ 分析正常
```

用户几乎不需要操作。

------

# 三十二、运行环境页面

```text
运行环境

.NET 运行环境

检测到：
.NET 10 Desktop Runtime x64


部署方式

● 智能离线部署                 推荐
  Runtime 随 Setup 一起发布
  目标电脑已有 Runtime 时自动跳过

○ 在线部署

○ Self-contained

○ 不处理 Runtime


Runtime

版本：10.0.xx
架构：x64

状态：
✓ 已缓存

windowsdesktop-runtime-10.0.xx-win-x64.exe
```

------

# 三十三、构建页面

用户点击：

```text
生成安装包
```

显示明确流程：

```text
构建 RHCVP 1.0.0

✓ 验证项目

✓ 分析应用
  .NET 10 Desktop
  x64

✓ 收集文件
  26 files
  18.4 MB

✓ Runtime
  使用本地缓存

✓ 生成 Inno Setup Script

✓ 编译安装包

✓ 验证安装包

○ 数字签名
  未启用


构建成功

RHCVP_Setup_1.0.0.exe

大小：103 MB
耗时：12.6 秒

[打开目录]
```

------

# 三十四、CLI 设计

以后 CI 非常有价值。

可执行文件：

```text
dotpack.exe
```

命令：

```text
dotpack init
```

创建配置。

```text
dotpack analyze RHCVP.exe
```

分析项目。

```text
dotpack restore RHCVP.pack.json
```

下载依赖。

```text
dotpack build RHCVP.pack.json
```

打包。

```text
dotpack clean
```

清理缓存。

以后 CI：

```text
dotnet publish
dotpack build RHCVP.pack.json
```

即可。

------

# 三十五、日志系统

分两个日志。

### 打包日志

```text
%LocalAppData%\DotNetPackager\Logs
```

记录：

```text
项目分析
Runtime 下载
模板生成
ISCC 输出
签名
错误
```

------

### 安装日志

安装器支持：

```text
安装程序启动
环境检查
.NET Runtime 状态
Runtime 安装
文件安装
升级
完成
```

客户电脑安装失败时，可以直接拿日志定位。

------

# 三十六、错误体系

不能全部：

```text
catch(Exception)
{
    MessageBox.Show("打包失败");
}
```

应该建立错误码：

```text
DP1001
未发现主程序

DP1002
runtimeconfig.json 缺失

DP1101
无法确定应用架构

DP2001
Runtime 下载失败

DP2002
Runtime 哈希校验失败

DP3001
Inno Setup 编译失败

DP4001
数字签名失败
```

UI：

```text
构建失败

DP3001
Inno Setup 编译失败

原因：
xxx.ico 文件不存在

文件：
installer.iss:32

[查看日志]
```

------

# 三十七、安全设计

Runtime 或其他官方依赖下载之后至少进行：

```text
SHA256/SHA512 校验
```

条件允许再做：

```text
Authenticode 签名检查
```

只有校验通过才进入缓存。

自定义 prerequisite 应明显提示：

```text
该组件将在客户计算机上以管理员权限执行。
```

避免工具悄无声息执行未知程序。

------

# 三十八、Build Workspace

每次构建创建独立临时工作区：

```text
%TEMP%
└─ DotNetPackager
   └─ build-{GUID}
      ├─ app
      ├─ prerequisites
      ├─ scripts
      ├─ output
      └─ logs
```

成功后：

```text
复制 Setup → dist
```

然后删除临时目录。

失败则可配置：

```text
☑ 保留构建目录用于调试
```

------

# 三十九、输出目录规范

项目：

```text
RHCVP
│
├─ RHCVP.csproj
├─ RHCVP.pack.json
│
└─ dist
   ├─ RHCVP_Setup_1.0.0.exe
   ├─ RHCVP_Setup_1.0.0.exe.sha256
   └─ build.json
```

其中：

```text
build.json
```

记录：

```text
版本
构建时间
Git Commit
.NET Runtime
文件 Hash
Packager 版本
```

对以后软件追溯非常有用。

------

# 四十、RHCVP 的实际打包流程

用你现在这个程序作为第一套测试样例。

当前：

```text
RHCVP.exe
RHCVP.dll
RHCVP.runtimeconfig.json
RHCVP.deps.json
LiveCharts
SkiaSharp
OpenTK
Data
Logs
...
```

导入后：

```text
ApplicationAnalyzer
        ↓
RHCVP.exe
        ↓
runtimeconfig
        ↓
net10.0
        ↓
Microsoft.WindowsDesktop.App
        ↓
.NET 10 Desktop Runtime
        ↓
x64
```

自动推荐：

```text
部署模式：
智能离线

前置组件：
.NET 10 Desktop Runtime x64

安装范围：
所有用户

安装位置：
C:\Program Files\RHCVP

桌面快捷方式：
是

开始菜单：
是

Logs：
不打入安装包

PDB：
不打入

Data：
打入

appsettings：
打入
```

然后生成：

```text
RHCVP_Setup_1.0.0.exe
```

------

# 四十一、目标电脑安装流程

最终用户双击：

```text
RHCVP_Setup_1.0.0.exe
```

内部：

```text
启动
 ↓
操作系统检查
 ↓
架构检查
 ↓
检查 .NET 10 Desktop x64
 ↓
 ┌─────────────┐
 │             │
存在           不存在
 │             │
 │        解压 Runtime
 │             ↓
 │        静默安装
 │             ↓
 │        检查结果
 │             │
 └──────┬──────┘
        ↓
安装 RHCVP
        ↓
创建快捷方式
        ↓
写卸载信息
        ↓
完成
        ↓
启动 RHCVP
```

用户不需要知道：

```text
什么叫 Runtime
什么叫 WPF
什么叫 Microsoft.WindowsDesktop.App
```

这正是工具应该解决的问题。

------

# 四十二、MVP 第一阶段

不要第一版什么都做。

第一阶段只实现：

```text
目录导入

↓

自动识别
EXE
runtimeconfig
.NET
WPF
架构

↓

文件筛选

↓

.NET Desktop Runtime
自动下载 + 缓存

↓

生成 Inno Setup

↓

Runtime 环境检测

↓

Runtime 离线安装

↓

安装程序

↓

快捷方式

↓

覆盖升级

↓

卸载

↓

Setup.exe
```

也就是说：

> 先完整解决你现在 RHCVP 的需求。

这是 MVP 最正确的边界。

------

# 四十三、第二阶段

MVP 稳定后加入：

```text
.csproj 导入
dotnet publish
Self-contained
Single-file
VC++ Runtime
WebView2
自定义 EXE/MSI 前置组件
数字签名
CLI
```

此时已经可以称为正式：

**V1.0**

------

# 四十四、第三阶段

面向公司内部工程化：

```text
GitHub Actions
GitLab CI
Jenkins
版本自动生成
Git Commit 记录
安装包校验文件
构建报告
多项目模板
批量打包
```

例如：

```text
提交代码
 ↓
编译
 ↓
测试
 ↓
dotnet publish
 ↓
DotNetPackager
 ↓
数字签名
 ↓
发布 Setup
```

------

# 四十五、第四阶段

再考虑：

```text
自动升级
增量更新
应用内升级
升级服务器
Release Channel

Stable
Beta
Dev
```

这一阶段可以吸收 Velopack 的设计。

不要放进第一版。

------

# 四十六、未来可以进一步发展成“上位机部署工具”

这实际上是我比较建议保留的方向。

最终不只是：

> .NET 打包器

而是：

> Windows 工业上位机软件部署管理工具。

例如一个项目：

```text
应用
RHCVP

运行环境
.NET 10 Desktop

驱动
NI-VISA
USB Driver
串口 Driver

第三方环境
VC++ Runtime

设备服务
DeviceService.exe

配置
appsettings.json

数据库
SQLite

安装完成
启动 RHCVP
```

最终依然只有：

```text
RHCVP_Setup.exe
```

这比单纯做一个 Inno Setup GUI 前端有价值得多。

------

# 四十七、第一版建议的最终模块清单

建议正式确定为下面 12 个模块：

```text
01 ProjectManager
   打包项目管理

02 ApplicationAnalyzer
   .NET 应用分析

03 PublishEngine
   dotnet publish

04 FileRuleEngine
   安装文件管理

05 RuntimeResolver
   .NET Runtime 分析

06 RuntimeCatalog
   Runtime 获取、下载、缓存

07 PrerequisiteManager
   前置组件管理

08 InstallerGenerator
   Inno Setup 脚本生成

09 InstallerCompiler
   Setup 编译

10 SigningService
   数字签名

11 BuildPipeline
   完整构建流程

12 CLI
   自动化打包
```

核心依赖关系保持：

```text
             UI
              │
              ▼
        BuildPipeline
              │
      ┌───────┼────────┐
      ▼       ▼        ▼
 Analyzer   Files    Runtime
      │                │
      └──────┬─────────┘
             ▼
     Package Definition
             │
             ▼
     InstallerGenerator
             │
             ▼
     InstallerCompiler
             │
             ▼
          Signer
             │
             ▼
         Setup.exe
```

这样不会乱。

------

# 四十八、开发优先级

如果现在真正开始开发，我建议严格按照这个顺序：

```text
P0
配置模型
ApplicationAnalyzer
runtimeconfig 解析
架构检测

↓

P1
RuntimeResolver
Runtime 下载与缓存

↓

P2
FileRuleEngine
Package Model

↓

P3
Inno Setup Template
ISCC 调用

↓

P4
.NET 环境检测脚本
Runtime 安装

↓

P5
WPF GUI

↓

P6
升级 / 卸载

↓

P7
日志 / 错误体系

↓

P8
数字签名

↓

P9
CLI
```

尤其不要第一步就画 GUI。

先把：

```text
RHCVP 文件夹
        ↓
程序分析结果
        ↓
安装模型
        ↓
installer.iss
        ↓
Setup.exe
```

这条核心链路跑通。

------

