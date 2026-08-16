# 编辑器插件

Estragonia 可以在 Godot 编辑器 Dock 里画 Avalonia。目标是：**在 IDE 里改插件界面，回到 Godot 后 Dock 显示新内容** — 不让 Godot 卸游戏程序集，也不重启 Avalonia。

## 约束

| 项 | 规则 |
|----|------|
| Godot 脚本 | `EditorPlugin`、`AvaloniaControl`、`AvaloniaEditorHost` 必须在 Godot 主工程，类名 = 文件名，`#if TOOLS` + `[Tool]` |
| 插件 View | 独立 `Microsoft.NET.Sdk` 工程。只引用 Estragonia。**不要** `Avalonia.Desktop` / Fluent（预览走 Preview），**不要** `ProjectReference` 进主工程，**不要** 用 Godot.NET.Sdk，程序集内 **禁止** `GodotObject` |
| 热重载 | 自管 collectible ALC + `host.Control = 新页面`；不是 Godot Build |
| Avalonia | 编辑器进程只 `SetupWithoutStarting` 一次；不要 `Shutdown` |
| 插件互相 | 直接 `: EditorPlugin`，不要抽象 `EditorPlugin` 基类 |

插件源码必须放在 Godot 工程树外（或 `Compile Remove`），否则切回编辑器会整包卸游戏 ALC，再次踩 [godot#78513](https://github.com/godotengine/godot/issues/78513)。

## 示例布局

```
samples/
  HelloWorld/                         Godot 工程
    addons/estragonia_editor/         仅薄壳：plugin.cfg + plugin.json + EditorPlugin
    addons/estragonia_editor_log/
    AvaloniaControl.cs / UiHost.cs / AvaloniaEditorHost.cs
  HelloWorld.Editor.Demo/             插件 View；输出复制到 .godot/estragonia-plugins/
  HelloWorld.Editor.Log/
  HelloWorld.Editor.Preview/          解决方案启动项目：桌面预览两个 Dock（不经过 Godot）
```

`plugin.json` 示例：

```json
{
  "id": "estragonia.demo",
  "assembly": ".godot/estragonia-plugins/HelloWorld.Editor.Demo.dll",
  "pageType": "HelloWorld.Editor.Demo.DemoPage"
}
```

Godot 主工程 **不要** 引用这两个 Editor 工程。解决方案仍可包含它们，方便 IDE 编译。

`addons/` **不能删** `EditorPlugin` 薄壳：Godot 要求 `plugin.cfg` 指向游戏程序集里的脚本（类名 = 文件名）。View / `IEditorPage` 不放 addons。`AvaloniaEditorHost` 放在 Godot 工程根目录，两个 Dock 共用，不要复制进每个 addon。

## 工作流

1. 在 IDE 把 **HelloWorld.Editor.Preview** 设为启动项目，F5 预览 Demo / Log。插件工程不引用桌面 Avalonia，不要在插件项目上开 XAML 预览器。
2. 用 Godot 4.7+（.NET）打开 `samples/HelloWorld`，启用两个 Estragonia 编辑器插件。
3. 先编译 `HelloWorld.Editor.Demo` / `Log`（编 Preview 也会带上），让 DLL 出现在 `.godot/estragonia-plugins/`。
4. 改 `DemoView.axaml` 后 **只编译该 Editor 工程**。Godot 打开时若旧 DLL 被占用，构建会另外写出一份带时间戳的副本；切回编辑器即可加载新文件，不必先关 Godot。
5. 切回 Godot，或点 Dock 上的「Reload this plugin」。
6. 左侧 Dock 换成新页面；Log 的 DLL 没变则不动。

不要为了刷新插件去点 Godot 锤子。改 `EstragoniaEditorPlugin.cs` / `AvaloniaEditorHost.cs` 仍须 Godot Build。

## API

- `AvaloniaEditorRuntime.EnsureStarted()` — 进程级 `EditorAvaloniaApp`（Fluent 深色），第二次是 no-op。
- `EditorPluginCatalog.RegisterManifest(path)` — 读取 `plugin.json`。
- `EditorPluginCatalog.Reload(id)` / `ReloadChanged()` / `UnloadAll()`。
- `IEditorPage` — 在插件程序集里实现，`Create()` 返回 `UserControl`。
- `EditorLogHub` — 框架侧日志缓冲。插件应快照读取，不要订自己类型上的静态事件。

工程内的 `[Tool]` 脚本 `AvaloniaEditorHost` 设置 `PluginId` 并向 Catalog 登记。`Detach()` 清掉 Avalonia 页面，保留 Godot 节点和 TopLevel。

## 明确不做

- Avalonia XAML Hot Reload（可后补；不替代程序集换页）
- 把 `EditorPlugin` 放进独立程序集
- 保证复杂视觉树 100% 被 GC。换页仍会成功；ALC 卸不掉只打警告。
