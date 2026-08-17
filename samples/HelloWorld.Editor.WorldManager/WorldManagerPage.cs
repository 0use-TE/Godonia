using Avalonia.Controls;
using Ouse.Godonia;

namespace HelloWorld.Editor.WorldManager;

public sealed class WorldManagerPage : IEditorPage {

	public string Id => "helloworld.world.manager";

	public string Title => "World Manager";

	public Control Create() => new WorldManagerView();

}