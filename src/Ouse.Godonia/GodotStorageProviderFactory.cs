using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Platform.Storage;

namespace Ouse.Godonia;

/// <summary>Implementation of <see cref="IStorageProviderFactory"/> for Godot.</summary>
internal sealed class GodotStorageProviderFactory : IStorageProviderFactory {

	public IStorageProvider CreateProvider(TopLevel topLevel)
		=> new GodotStorageProvider();

}
