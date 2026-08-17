# 输入与渲染

## 渲染

Avalonia 画到与 Godot 共享的 Vulkan 纹理（`Texture2Drd`），由宿主 `Control._Draw` 贴出。没有单独的 Avalonia「窗口层」——层级跟 Godot 场景树 / `z_index` 走。

## 输入顺序

```text
Node._Input
  → Control._GuiInput（Avalonia 宿主，若 _HasPoint）
  → Node._UnhandledInput
```

- 宿主收到的指针事件通常会 `AcceptEvent()`，后面的 GUI / unhandled 看不到。
- 更早的 `_Input` 若 `SetInputAsHandled()`，Avalonia 可能收不到。

## 主题

Avalonia `RequestedThemeVariant` Light/Dark 可用。Godonia 的平台「系统」主题目前通过 `GodotPlatformSettings` 默认 Dark。
