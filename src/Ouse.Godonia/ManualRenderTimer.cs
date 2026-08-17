using System;
using Avalonia.Rendering;

namespace Ouse.Godonia;

/// <summary>A <see cref="IRenderTimer"/> implementation that is only triggered manually.</summary>
internal sealed class ManualRenderTimer : IRenderTimer {

	public Action<TimeSpan>? Tick { get; set; }

	bool IRenderTimer.RunsInBackground
		=> false;

	public void TriggerTick(TimeSpan elapsed)
		=> Tick?.Invoke(elapsed);

}
