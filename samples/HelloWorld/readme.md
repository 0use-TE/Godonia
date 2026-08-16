# HelloWorld

Estragonia 场景演示（左侧列表切换）。

## 场景

| 场景 | 内容 |
|------|------|
| 总览 | 简介与推荐体验顺序 |
| 基础控件 | TextBox / ComboBox / Slider / Toggle / Expander 等 |
| 自定义光标 | 标准 CursorShape + `CreateCursor(Bitmap)` 位图光标 |
| 游戏 HUD | HP/MP/EXP、金币、波次、战斗日志 |
| 输入穿透 | 透明区把鼠标交给背后 Godot Sprite |
| 数据绑定 | MVVM 列表增删 |

## 宿主脚本

Godot 节点类型必须在本工程内：

- `AvaloniaControl.cs` / `UiHost.cs` — 游戏场景宿主
- `AvaloniaEditorHost.cs` — 编辑器 Dock 宿主（两个插件共用）
- `UserInterface.cs` → `CreateRoot()`

`addons/` 里只留 Godot 薄壳：`plugin.cfg` + `plugin.json` + `EditorPlugin` 脚本。界面在独立工程，不要写进 addons。

## 编辑器 Dock 与预览

| 工程 | 作用 |
|------|------|
| `HelloWorld.Editor.Preview` | **启动项目**：桌面预览 Demo / Log（不经过 Godot） |
| `HelloWorld.Editor.Demo` / `Log` | 插件 View；改完只编译该工程 |
| `HelloWorld` | Godot 游戏 + 薄壳 EditorPlugin |

在 IDE 把 **HelloWorld.Editor.Preview** 设为启动项目后 F5。插件工程不引用 `Avalonia.Desktop`，不要在插件项目上开 XAML 预览器。

Godot 里看 Dock：先编 Demo/Log，再用 Godot 打开本目录。切回 Godot 会换页，不要点锤子。详见 [编辑器插件](../../docs/v1.0.0/zh-CN/editor-plugins.md)。

## 运行

1. 用 **Godot 4.7+（.NET）** 打开本目录。
2. 运行主场景。
3. 切到「输入穿透」或「自定义光标」验证对应能力。
