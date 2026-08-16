using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Platform;
using Godot;
using JLeb.Estragonia.Input;
using AvControl = Avalonia.Controls.Control;
using GdControl = Godot.Control;
using GdInput = Godot.Input;
using GdKey = Godot.Key;

namespace JLeb.Estragonia;

/// <summary>
/// Avalonia render / input engine used by the Godot-project <c>AvaloniaControl</c> script.
/// Stays in this assembly so it can use Avalonia private platform APIs; the Godot node subclass itself must live in your Godot project.
/// </summary>
public sealed class AvaloniaControlEngine : IDisposable {

	private static readonly object s_instancesLock = new();
	private static readonly List<WeakReference<AvaloniaControlEngine>> s_instances = [];

	private GdControl? _owner;
	private AvControl? _control;
	private double _renderScaling = 1.0;
	private GodotTopLevel? _topLevel;
	private bool _disposed;
	private bool _eventsHooked;

	public AvaloniaControlEngine(GdControl owner) {
		_owner = owner ?? throw new ArgumentNullException(nameof(owner));
		lock (s_instancesLock)
			s_instances.Add(new WeakReference<AvaloniaControlEngine>(this));
	}

	/// <summary>
	/// Tears down every live TopLevel before Godot unloads the game collectible ALC.
	/// The engine type lives in this persistent assembly; <c>_owner</c> does not.
	/// </summary>
	public static void DisposeAll() {
		AvaloniaControlEngine[] live;
		lock (s_instancesLock) {
			var found = new List<AvaloniaControlEngine>();
			for (var i = s_instances.Count - 1; i >= 0; i--) {
				if (!s_instances[i].TryGetTarget(out var engine) || engine._disposed) {
					s_instances.RemoveAt(i);
					continue;
				}

				found.Add(engine);
			}

			live = found.ToArray();
		}

		foreach (var engine in live)
			engine.Dispose();

		if (live.Length > 0)
			GD.Print($"Estragonia: disposed {live.Length} Avalonia top-level(s) for assembly reload.");
	}

	/// <summary>Gets or sets the underlying Avalonia control that will be rendered.</summary>
	public AvControl? Control {
		get => _control;
		set {
			if (_disposed || ReferenceEquals(_control, value))
				return;

			if (value is not null)
				GodotPlatform.EnsureAssetLoader(value.GetType().Assembly);
			else
				GodotPlatform.EnsureAssetLoader(typeof(EditorAvaloniaApp).Assembly);

			_control = value;

			if (_topLevel is not null)
				_topLevel.Content = _control;
		}
	}

	/// <summary>Gets or sets the render scaling for the Avalonia control. Defaults to 1.0.</summary>
	[SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator", Justification = "Doesn't affect correctness")]
	public double RenderScaling {
		get => _renderScaling;
		set {
			if (_renderScaling == value)
				return;

			_renderScaling = value;
			OnResized();
			_owner?.QueueRedraw();
		}
	}

	/// <summary>
	/// Gets or sets whether some Godot UI actions will be automatically mapped to an <see cref="InputElement.KeyDownEvent"/> event.
	/// The mapped actions are ui_left, ui_right, ui_up, ui_down, ui_accept and ui_cancel.
	/// Defaults to true.
	/// </summary>
	public bool AutoConvertUIActionToKeyDown { get; set; } = true;

	/// <summary>
	/// When false (default), only Avalonia-hittable pixels capture the mouse and empty/transparent
	/// areas pass through to Godot nodes behind. When true, the whole control rect captures input.
	/// </summary>
	public bool CaptureEmptyHits { get; set; }

	/// <summary>Gets the underlying Avalonia top-level element.</summary>
	public GodotTopLevel GetTopLevel()
		=> _topLevel ?? throw new InvalidOperationException($"The {nameof(AvaloniaControlEngine)} isn't initialized");

	/// <summary>Gets the underlying Godot texture where <see cref="Control"/> is rendered.</summary>
	public Texture2D GetTexture()
		=> GetTopLevel().Impl.GetGdTexture();

	/// <summary>Whether the Avalonia top-level has been created.</summary>
	public bool IsInitialized
		=> _topLevel is not null && !_disposed;

	public void Ready() {
		if (_disposed || _owner is not { } owner)
			return;

		if (_topLevel is not null) {
			NotifyResized();
			return;
		}

		// Game hosts and editor docks share this path once UseGodot() has run.

		// Skia outputs a premultiplied alpha image, ensure we got the correct blend mode if the user didn't specify any
		owner.Material ??= new CanvasItemMaterial {
			BlendMode = CanvasItemMaterial.BlendModeEnum.PremultAlpha,
			LightMode = CanvasItemMaterial.LightModeEnum.Unshaded
		};

		var locator = AvaloniaLocator.Current;

		if (locator.GetService<IPlatformGraphics>() is not GodotVkPlatformGraphics graphics) {
			GD.PrintErr("No Godot platform graphics found, did you forget to register your Avalonia app with UseGodot()?");
			return;
		}

		var topLevelImpl = new GodotTopLevelImpl(graphics, locator.GetRequiredService<IClipboard>(), GodotPlatform.Compositor) {
			CursorChanged = OnAvaloniaCursorChanged
		};

		topLevelImpl.SetRenderSize(GetFrameSize(), RenderScaling);

		_topLevel = new GodotTopLevel(topLevelImpl) {
			Background = null,
			Content = Control,
			TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent, WindowTransparencyLevel.None }
		};

		_topLevel.Prepare();
		_topLevel.StartRendering();

		// Game: C# events are fine. Editor: those become dead ManagedCallables after
		// assembly reload — AvaloniaControl forwards Node notifications instead.
		if (!Engine.IsEditorHint() && !_eventsHooked) {
			owner.Resized += OnResized;
			owner.FocusEntered += OnFocusEntered;
			owner.FocusExited += OnFocusExited;
			owner.MouseExited += OnMouseExited;
			_eventsHooked = true;
		}

		if (owner.HasFocus())
			OnFocusEntered();
	}

	public void NotifyResized()
		=> OnResized();

	public void NotifyFocusEntered()
		=> OnFocusEntered();

	public void NotifyFocusExited()
		=> _topLevel?.Impl.OnLostFocus();

	public void NotifyMouseExited()
		=> _topLevel?.Impl.OnMouseExited(Time.GetTicksMsec());

	public void Process() {
		if (_disposed || _topLevel is null)
			return;

		GodotPlatform.TriggerRenderTick();

		// We might have cleared the texture after resize to prevent corruption on AMD GPU (see GodotSkiaGpuRenderSession),
		// force a re-render.
		if (_topLevel?.Impl.SurfaceDrawCount <= 2)
			RenderAvalonia();
	}

	public void Draw() {
		if (_topLevel is null || _owner is not { } owner)
			return;

		owner.DrawTexture(_topLevel.Impl.GetGdTexture(), Vector2.Zero);
	}

	public void GuiInput(InputEvent @event) {
		if (_topLevel is null)
			return;

		var handled = TryHandleInput(_topLevel.Impl, @event) || TryHandleAction(@event);

		// Always consume pointer events while the cursor is over this control.
		// Avalonia often leaves RawPointerEventArgs.Handled == false; without AcceptEvent,
		// Godot can let the 3D viewport steal the mouse after Button press.
		if (handled
			|| @event is InputEventMouseButton
			|| @event is InputEventMouseMotion
			|| @event is InputEventScreenTouch
			|| @event is InputEventScreenDrag) {
			_owner?.AcceptEvent();
		}
	}

	public bool HasPoint(Vector2 point) {
		if (_owner is not { } owner)
			return false;

		var size = owner.Size;
		if (point.X < 0f || point.Y < 0f || point.X > size.X || point.Y > size.Y)
			return false;

		if (_topLevel is null)
			return CaptureEmptyHits;

		var avaloniaPoint = point.ToAvaloniaPoint() / _topLevel.RenderScaling;
		if (_topLevel.InputHitTest(avaloniaPoint, false) is not null)
			return true;

		return CaptureEmptyHits;
	}

	private PixelSize GetFrameSize()
		=> PixelSize.FromSize((_owner?.Size ?? default).ToAvaloniaSize(), 1.0);

	private void RenderAvalonia()
		=> _topLevel!.Impl.OnDraw(new Rect((_owner?.Size ?? default).ToAvaloniaSize()));

	private bool _hasCustomMouseCursor;

	private void OnAvaloniaCursorChanged(ICursorImpl? cursor) {
		if (_owner is not { } owner)
			return;

		if (cursor is GodotCustomCursorImpl custom) {
			GdInput.SetCustomMouseCursor(custom.Texture, GdInput.CursorShape.Arrow, custom.Hotspot);
			_hasCustomMouseCursor = true;
			owner.MouseDefaultCursorShape = GdControl.CursorShape.Arrow;
			return;
		}

		if (_hasCustomMouseCursor) {
			GdInput.SetCustomMouseCursor(null);
			_hasCustomMouseCursor = false;
		}

		owner.MouseDefaultCursorShape =
			(cursor as GodotStandardCursorImpl)?.CursorShape ?? GdControl.CursorShape.Arrow;
	}

	private void OnResized() {
		if (_topLevel is null)
			return;

		_topLevel.Impl.SetRenderSize(GetFrameSize(), RenderScaling);
		RenderAvalonia();
	}

	private void OnFocusEntered() {
		if (_topLevel is null)
			return;

		_topLevel.Focus();

		if (KeyboardNavigationHandler.GetNext(_topLevel, NavigationDirection.Next) is not { } inputElement)
			return;

		NavigationMethod navigationMethod;

		if (GdInput.IsActionPressed(GodotBuiltInActions.UIFocusNext) || GdInput.IsActionPressed(GodotBuiltInActions.UIFocusPrev))
			navigationMethod = NavigationMethod.Tab;
		else if (GdInput.GetMouseButtonMask() != 0)
			navigationMethod = NavigationMethod.Pointer;
		else
			navigationMethod = NavigationMethod.Unspecified;

		inputElement.Focus(navigationMethod);
	}

	private void OnFocusExited()
		=> _topLevel?.Impl.OnLostFocus();

	private bool TryHandleAction(InputEvent inputEvent) {
		if (!inputEvent.IsActionType())
			return false;

		if (inputEvent.IsActionPressed(GodotBuiltInActions.UIFocusNext, true, true))
			return TryMoveFocus(NavigationDirection.Next, inputEvent);

		if (inputEvent.IsActionPressed(GodotBuiltInActions.UIFocusPrev, true, true))
			return TryMoveFocus(NavigationDirection.Previous, inputEvent);

		if (AutoConvertUIActionToKeyDown) {

			if (inputEvent.IsActionPressed(GodotBuiltInActions.UILeft, true, true))
				return SimulateKeyDownFromAction(inputEvent, GdKey.Left);

			if (inputEvent.IsActionPressed(GodotBuiltInActions.UIRight, true, true))
				return SimulateKeyDownFromAction(inputEvent, GdKey.Right);

			if (inputEvent.IsActionPressed(GodotBuiltInActions.UIUp, true, true))
				return SimulateKeyDownFromAction(inputEvent, GdKey.Up);

			if (inputEvent.IsActionPressed(GodotBuiltInActions.UIDown, true, true))
				return SimulateKeyDownFromAction(inputEvent, GdKey.Down);

			if (inputEvent.IsActionPressed(GodotBuiltInActions.UIAccept, true, true))
				return SimulateKeyDownFromAction(inputEvent, GdKey.Enter);

			if (inputEvent.IsActionPressed(GodotBuiltInActions.UICancel, true, true))
				return SimulateKeyDownFromAction(inputEvent, GdKey.Escape);

		}

		return false;
	}

	private bool SimulateKeyDownFromAction(InputEvent inputEvent, GdKey key) {
		// if the action already matches the key we're going to simulate, abort: it already got through TryHandleInput and wasn't handled
		if (inputEvent is InputEventKey inputEventKey && inputEventKey.Keycode == key)
			return false;

		if (_topLevel?.FocusManager?.GetFocusedElement() is not { } currentElement)
			return false;

		var args = new KeyEventArgs {
			RoutedEvent = InputElement.KeyDownEvent,
			Key = key.ToAvaloniaKey(),
			KeyModifiers = inputEvent.GetKeyModifiers()
		};
		currentElement.RaiseEvent(args);
		return args.Handled;
	}

	private static bool TryHandleInput(GodotTopLevelImpl impl, InputEvent inputEvent)
		=> inputEvent switch {
			InputEventMouseMotion mouseMotion => impl.OnMouseMotion(mouseMotion, Time.GetTicksMsec()),
			InputEventMouseButton mouseButton => impl.OnMouseButton(mouseButton, Time.GetTicksMsec()),
			InputEventScreenTouch screenTouch => impl.OnScreenTouch(screenTouch, Time.GetTicksMsec()),
			InputEventScreenDrag screenDrag => impl.OnScreenDrag(screenDrag, Time.GetTicksMsec()),
			InputEventKey key => impl.OnKey(key, Time.GetTicksMsec()),
			InputEventJoypadButton joypadButton => impl.OnJoypadButton(joypadButton, Time.GetTicksMsec()),
			InputEventJoypadMotion joypadMotion => impl.OnJoypadMotion(joypadMotion, Time.GetTicksMsec()),
			_ => false
		};

	private bool TryMoveFocus(NavigationDirection direction, InputEvent inputEvent) {
		if (_topLevel?.FocusManager is not { } focusManager)
			return false;

		var currentElement = focusManager.GetFocusedElement() ?? _topLevel;

		// GodotTopLevel has a Continue tab navigation since we want to be able to focus the Godot controls
		// once we're done with the Avalonia ones. However, if there's no Godot control, we want to act as Cycle.
		var nextElement = GetNextTabElement(currentElement, direction);
		if (nextElement is null) {
			var nextGdControl = _owner is not { } owner
				? null
				: direction switch {
					NavigationDirection.Next => owner.FindNextValidFocus(),
					NavigationDirection.Previous => owner.FindPrevValidFocus(),
					_ => null
				};

			if ((nextGdControl is null || nextGdControl == _owner) && (object) currentElement != _topLevel)
				nextElement = GetNextTabElement(_topLevel, direction);
		}


		if (nextElement is null)
			return false;

		nextElement.Focus(NavigationMethod.Tab, inputEvent.GetKeyModifiers());
		return true;
	}

	private static IInputElement? GetNextTabElement(IInputElement element, NavigationDirection direction) {
		var previous = element;

		while (true) {
			// GetNext doesn't take IsEffectivelyEnabled into account, check it manually
			var next = KeyboardNavigationHandler.GetNext(previous, direction);
			if (next is null || next.IsEffectivelyEnabled)
				return next;

			// handle potential all-disabled cycle
			if (next == element)
				return null;

			previous = next;
		}
	}

	private void OnMouseExited()
		=> _topLevel?.Impl.OnMouseExited(Time.GetTicksMsec());

	public void Dispose() {
		if (_disposed)
			return;

		_disposed = true;

		if (_eventsHooked && _owner is not null) {
			try {
				_owner.Resized -= OnResized;
				_owner.FocusEntered -= OnFocusEntered;
				_owner.FocusExited -= OnFocusExited;
				_owner.MouseExited -= OnMouseExited;
			}
			catch {
				// Owner may already be a disposed GodotObject during ALC unload.
			}

			_eventsHooked = false;
		}

		if (_topLevel is not null) {
			try {
				_topLevel.Content = null;
				_topLevel.Impl.CursorChanged = null;
				// Do not clear Closed: TopLevel.HandleClosed stops MediaContext rendering.
				_topLevel.StopRendering();
				_topLevel.Dispose();
			}
			catch (Exception ex) {
				GD.PrintErr($"Estragonia: TopLevel dispose failed: {ex.Message}");
			}

			_topLevel = null;
		}

		_control = null;
		_owner = null;
		GodotPlatform.EnsureAssetLoader(typeof(EditorAvaloniaApp).Assembly);
	}

}
