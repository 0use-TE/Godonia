using System;
using Avalonia;

namespace GodotGame.Editor.Preview;

internal static class Program {

	[STAThread]
	public static void Main(string[] args)
		=> BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

	// Avalonia designer / VS previewer entry point
	public static AppBuilder BuildAvaloniaApp()
		=> AppBuilder
			.Configure<App>()
			.UsePlatformDetect()
			.UseSkia()
			.LogToTrace();

}
