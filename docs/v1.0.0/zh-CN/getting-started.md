# 快速开始

## 标识

| | |
|--|--|
| 产品名 | **Godonia** |
| 包名 / 命名空间 | **`Ouse.Godonia`** |
| 包版本 | **1.0.0** |
| 包源 | 编辑器本地：`GodotSharp/Godonia/nupkg`（默认不走 nuget.org） |

> **含 AI 辅助代码，不保证稳定性**，请自行测试后再用于正式环境。  
> 基于 Julien Lebosquain 的 [Estragonia](https://github.com/MrJul/Estragonia)（MIT）。

## 环境

- **Godonia 自定义 Godot 编辑器**（本仓库：`godot/bin/godot.windows.editor.x86_64.mono.exe`）
- 渲染器 **Forward+** 或 **Mobile**（Vulkan）
- .NET SDK **10**
- Avalonia **12**

---

## 教程 A — 用 Godonia 编辑器新建工程（推荐）

### 1. 启动自定义编辑器

```text
godot/bin/godot.windows.editor.x86_64.mono.exe
```

exe 旁应有：

```text
GodotSharp/Godonia/
  runtime/          Ouse.Godonia.dll（编辑器宿主）
  nupkg/            Ouse.Godonia.1.0.0.nupkg
  project-template/ 新建工程时复制的 Avalonia 模板
```

刷新这些目录：

```powershell
.\scripts\deploy-godonia-editor.ps1
```

### 2. 新建项目

在项目管理器中新建 **Godot C# 项目**。编辑器会安装 Godonia Avalonia 模板（含 `.slnx`、游戏工程、`{Game}.Editor.Preview` 的嵌套工作区）。

模板已配置：

- Autoload `AvaloniaLoader` → 只初始化一次 `EnsureStarted` / `UseGodot()`
- **工程内已带 `AvaloniaControl.cs` + `UiHost.cs`**（必需宿主脚本）
- `UserInterface : UiHost` → `CreateRoot()`
- `nuget.config` 指向编辑器本地 `Godonia/nupkg`
- `{Game}.Editor.Preview` 供 Avalonia 桌面设计器 / F5 预览编辑器页面

### 3. 改游戏 UI

- 视图：`Views/MainView.axaml`
- 视图模型：`ViewModels/MainViewModel.cs`
- 主题：`App.axaml`

用**同一份**自定义编辑器打开 `project.godot`。不要打开 `.godot/` 缓存目录。

---

## 教程 B — 给已有 Godot C# 工程加包

先把 NuGet 源指到编辑器旁的 nupkg 目录（绝对路径），再：

```bash
dotnet add package Ouse.Godonia --version 1.0.0
dotnet build
```

`dotnet build` 会在缺少 `AvaloniaControl.cs` / `UiHost.cs` 时插入到 csproj 旁边（首次警告 `GODONIA001`）。请重载 Godot 以生成 `.uid`。

类型仍编进 **你的 Godot 程序集**。不要把它们挪到类库。若不想自动插入：设 `GodoniaInjectHostScripts` 为 `false`，再从包内 `host-scripts/` 手拷。

### 1. Avalonia `Application` + 主题

准备 `App.axaml` / `App.axaml.cs`（如 Semi 或 Fluent）。

### 2. Autoload（整个进程只启动一次）

```csharp
using Godot;
using Ouse.Godonia;

public partial class AvaloniaLoader : Node
{
    public override void _Ready()
    {
        if (Engine.IsEditorHint())
            GodotAvalonia.EnsureStarted();
        else
            GodotAvalonia.EnsureStarted<App>();

        GetWindow()?.SetImeActive(true);
    }
}
```

在 `project.godot` 里注册为 Autoload。`EnsureStarted` 若已启动则是 no-op——不要 `Shutdown` 再启动。

### 3. 场景宿主脚本

```csharp
using Avalonia.Controls;
using Ouse.Godonia;

public partial class UserInterface : UiHost
{
    protected override Control CreateRoot()
        => new MainView { DataContext = new MainViewModel() };
}
```

把 `UserInterface.cs` 挂到主场景里铺满的 `Control` 上。

完整文件清单见 [宿主与 UI](hosting.md)。

---

## 本仓库示例

用 Godonia 编辑器打开 `samples/HelloWorld`（通过工程引用本地库源码）。  
示例里已经包含 `AvaloniaControl.cs`、`UiHost.cs`，以及 `godonia_new_plugin` 下三个编辑器页面：**技能树**（左侧）、**任务管理**（底部）、**世界管理**（MainScreen）。把 `HelloWorld.Editor.Preview` 设为启动项目可桌面页签预览；详见 [编辑器插件](editor-plugins.md)。

## 热重载

`AvaloniaControl` / `UiHost` 已放在 Godot 工程内，可按普通脚本热重载。

若 Avalonia 等库拖住程序集，仍可能出现 **Failed to unload assemblies**。编辑器卡死时：完全重启 Godot；必要则删 `.godot` 再开。

## 声明

本仓库含 AI 辅助改动，请自行验证后再用于正式环境。
