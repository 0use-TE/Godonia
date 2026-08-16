using System;
using System.Threading;
using Avalonia.Platform;

namespace JLeb.Estragonia;

/// <summary>Godot Vulkan-based <see cref="IPlatformGraphics"/> implementation.</summary>
public sealed class GodotVkPlatformGraphics : IPlatformGraphics, IDisposable {

	private GodotVkSkiaGpu? _context;
	private int _refCount;

	bool IPlatformGraphics.UsesSharedContext
		=> true;

	internal GodotVkSkiaGpu GetSharedContext() {
		if (_context is null || _context.IsLost) {
			_context?.Dispose();
			_context = null;
			_context = new GodotVkSkiaGpu();
		}

		return _context;
	}

	IPlatformGraphicsContext IPlatformGraphics.CreateContext()
		=> throw new NotSupportedException();

	IPlatformGraphicsContext IPlatformGraphics.GetSharedContext()
		=> GetSharedContext();

	public void AddRef()
		=> Interlocked.Increment(ref _refCount);

	public void Release() {
		Interlocked.Decrement(ref _refCount);
		// Do not Dispose when the last TopLevel goes away. Avalonia's compositor still
		// holds this instance; F5 recreates TopLevels on the same RenderingDevice.
	}


	public void Dispose() {
		if (_context is not null) {
			_context.Dispose();
			_context = null;
		}
	}
}
