# 宿主与 UI

## 硬性规则 — 宿主脚本必须在 Godot 工程内

`AvaloniaControl` 和 `UiHost` 是 **Godot 节点脚本**，必须以 `.cs` 文件形式放在你的 **Godot C# 工程里**（和 `project.godot` 同一程序集）。

| 放哪 | 可以吗 |
|------|--------|
| 你的 Godot 工程（`res://AvaloniaControl.cs`、`res://UiHost.cs` …） | 可以 |
| 从 `editor/project-template` 或 `samples/HelloWorld` 复制 | 可以 |
| 只引用 `Ouse.Godonia` NuGet / 库程序集 | **不行** |

Godot C# 编辑器热重载 **不支持** 仅存在于外部程序集（NuGet / `ProjectReference`）里的 Godot 节点类型。这是引擎限制（[godot#111881](https://github.com/godotengine/godot/issues/111881)、[godot#98094](https://github.com/godotengine/godot/issues/98094)）。

NuGet 包（`Ouse.Godonia`）提供平台桥接（`UseGodot`、Vulkan/Skia、`AvaloniaControlEngine` 等）。这两个 `.cs` **仍然必须编进 Godot 工程程序集**（类名 = 文件名）。来源可以是：

1. 编辑器工程模板 / HelloWorld 示例（已带）
2. 引用包后 **`dotnet build` 自动插入**（工程里还没有这两个文件时）
3. 从包内 `host-scripts/` 手拷

插入的是 **工程文件**，会进 git，这是预期。包升级 **不会** 覆盖已有文件。若宿主 API 有 breaking，请对照 `src/Ouse.Godonia/host-scripts/` 手动合并。

关闭自动插入：

```xml
<GodoniaInjectHostScripts>false</GodoniaInjectHostScripts>
```

扩展库即使引用了 `Ouse.Godonia`，也 **不要** 再 Pack 或插入这两文件；游戏 Godot 工程通过直接或传递引用获得插入。插入后请重载 Godot 以生成 `.uid`。

## 职责拆分

| 事情 | 放哪 |
|------|------|
| `UseGodot` / 资源 / IME | Autoload 一次（`AvaloniaLoader`） |
| 渲染 + 输入宿主 | 工程内的 `AvaloniaControl.cs` |
| 焦点 / `CreateRoot` | 工程内的 `UiHost.cs` |
| 场景脚本 | `UserInterface : UiHost` → 实现 `CreateRoot()` |
| 主题 / 全局样式 | Avalonia `Application` |

不要在 Avalonia `App` 里调用 `GrabFocus` / `GetWindow`。

## 工程里必须有的文件

```
YourGodotProject/
├── AvaloniaControl.cs   ← 模板、自动插入或手拷（不能省）
├── UiHost.cs            ← 模板、自动插入或手拷（不能省）
├── AvaloniaLoader.cs    ← Autoload
├── UserInterface.cs     ← 你的宿主：CreateRoot()
├── App.axaml (+ .cs)
└── Views/ …
```

## 最小可运行示例

```csharp
// UiHost.cs — 必须在 Godot 工程内
namespace Ouse.Godonia;

public abstract partial class UiHost : AvaloniaControl
{
    protected abstract Control CreateRoot();

    public override void _Ready()
    {
        FocusMode = FocusModeEnum.All;
        MouseFilter = MouseFilterEnum.Stop;
        Control = CreateRoot();
        base._Ready();
        GrabFocus();
    }
}
```

```csharp
// UserInterface.cs — 挂到场景上的脚本
using Ouse.Godonia;

public partial class UserInterface : UiHost
{
    protected override Control CreateRoot()
        => new MainView { DataContext = new MainViewModel() };
}
```

## 命中测试

默认 `CaptureEmptyHits = false`：只有 Avalonia 命中到的像素吃鼠标，空白可穿透到 Godot。

设 `CaptureEmptyHits = true` 则整块控件矩形都吃输入。

编辑器 Dock / MainScreen（独立插件 ALC、不重启 Avalonia）见 [编辑器插件](editor-plugins.md)。
