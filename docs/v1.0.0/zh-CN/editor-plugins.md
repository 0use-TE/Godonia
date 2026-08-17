# 编辑器插件

Godonia 可以在 Godot **Dock** 与 **MainScreen** 里画 Avalonia。目标是：**在 IDE 里改插件界面，回到 Godot 后显示新内容** — 不让 Godot 卸游戏程序集，也不重启 Avalonia。

用 Godonia 编辑器新建的工程会带上 **`godonia_new_plugin`** 宿主与向导 Dock。HelloWorld 则展示手写同类结构。

## 约束

| 项 | 规则 |
|----|------|
| Godot 脚本 | `EditorPlugin`、`AvaloniaControl`、`AvaloniaEditorHost` 必须在 Godot 主工程，类名 = 文件名，`#if TOOLS` + `[Tool]` |
| 插件 View | 独立 `Microsoft.NET.Sdk` 工程。只引用 **Ouse.Godonia**。**不要** `Avalonia.Desktop` / Fluent（预览走 Preview），**不要** `ProjectReference` 进主工程，**不要** 用 Godot.NET.Sdk，程序集内 **禁止** `GodotObject` |
| 热重载 | 自管 collectible ALC + `host.Control = 新页面`；不是 Godot Build |
| Avalonia | 编辑器进程只 `SetupWithoutStarting` 一次；不要 `Shutdown` |
| 插件互相 | 直接 `: EditorPlugin`，不要抽象 `EditorPlugin` 基类 |

插件源码必须放在 Godot 工程树外（或 `Compile Remove`），否则切回编辑器会整包卸游戏 ALC，再次踩 [godot#78513](https://github.com/godotengine/godot/issues/78513)。

## 布局（HelloWorld 示例）

```
samples/
  HelloWorld/                              Godot 工程
    addons/godonia_new_plugin/             宿主 + 向导 + pages/*.json
    AvaloniaControl.cs / UiHost.cs / AvaloniaEditorHost.cs
  HelloWorld.Editor.SkillTree/             左侧 Dock — 技能树
  HelloWorld.Editor.QuestManager/          底部 Dock — 任务列表
  HelloWorld.Editor.WorldManager/          MainScreen — 区域流送工具
  HelloWorld.Editor.Preview/               F5 桌面页签预览三个拓展
```

HelloWorld 在 `addons/godonia_new_plugin/pages/` 下放了三份清单。新建游戏工程用同一宿主；编辑器内向导可继续脚手架。

`plugin.json` / pages 清单示例：

```json
{
  "id": "mygame.inspector",
  "assembly": ".godot/godonia-plugins/MyGame.Editor.Inspector.dll",
  "pageType": "MyGame.Editor.Inspector.InspectorPage",
  "dockSlot": "LeftUL"
}
```

`dockSlot` 可以是 Dock 位置（`LeftUL`、`RightUL` …），也可以是 **`MainScreen`**（顶部主屏按钮 / 标签）。

Godot 主工程 **不要** 引用 Editor 工程。解决方案仍可包含它们，方便 IDE 编译。

`AvaloniaEditorHost` 放在 Godot 工程根目录共用，不要复制进每个 addon。

## 向导（脚手架）

启用 `godonia_new_plugin` 后：

1. 用 **Godonia** 向导 Dock 创建页面（名称、Dock / MainScreen）。
2. 脚手架会生成同级 `{Game}.Editor.{Name}` 工程、`addons/godonia_new_plugin/pages/` 清单，并按需给 **Preview** 加 `ProjectReference`。
3. 编译 Editor 工程，让 DLL 落在 `.godot/godonia-plugins/`。
4. 切回 Godot（或 reload），宿主读取新清单。

## Preview 工程

`{Game}.Editor.Preview` 是 **WinExe** Avalonia 桌面应用（不是 Godot.NET.Sdk）。设为解决方案启动项目后 F5，即可不打开 Godot 预览 Dock / MainScreen 页面。

- 插件工程 **不要** 引用 `Avalonia.Desktop`；不要单独在插件工程上开 XAML 预览器。
- 生成的视图含 `mc:Ignorable="d"` 设计尺寸，方便 Preview 设计器。

## 工作流

1. 把 **`{Game}.Editor.Preview`** 设为启动项目，F5 预览页面。
2. 用 **Godonia** 编辑器打开 Godot 工程，按需启用 `godonia_new_plugin`。
3. 先编译 Editor 插件工程（编 Preview 也会带上），让 DLL 出现在 `.godot/godonia-plugins/`。
4. 改视图 `.axaml` 后 **只编译该 Editor 工程**。Godot 打开时若旧 DLL 被占用，构建会另外写出带时间戳的副本；切回编辑器即可加载。
5. 切回 Godot 或走宿主 reload。
6. 只有变更的插件页面会更新。

不要为了刷新插件去点 Godot 锤子。改薄壳 `EditorPlugin` / `AvaloniaEditorHost` 仍须 Godot Build。

## API

- `AvaloniaEditorRuntime.EnsureStarted()` — 进程级 `EditorAvaloniaApp`（Fluent 深色），第二次是 no-op。
- `EditorPluginCatalog.RegisterManifest(path)` — 读取清单 JSON。
- `EditorPluginCatalog.Reload(id)` / `ReloadChanged()` / `UnloadAll()`。
- `IEditorPage` — 在插件程序集里实现，`Create()` 返回 `UserControl`。
- `EditorPluginScaffolder` — 创建同级 Editor 工程与清单（向导使用）。
- `EditorLogHub` — 框架侧日志缓冲。插件应快照读取，不要订自己类型上的静态事件。

工程内的 `[Tool]` 脚本 `AvaloniaEditorHost` 设置 `PluginId` 并向 Catalog 登记。`Detach()` 清掉 Avalonia 页面，保留 Godot 节点和 TopLevel。

## 明确不做

- Avalonia XAML Hot Reload（可后补；不替代程序集换页）
- 把 `EditorPlugin` 放进独立程序集
- 保证复杂视觉树 100% 被 GC。换页仍会成功；ALC 卸不掉只打警告。
