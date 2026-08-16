using Avalonia.Controls;
using JLeb.Estragonia;

namespace HelloWorld.Editor.Demo;

public sealed class DemoPage : IEditorPage {

	public string Id
		=> "estragonia.demo";

	public string Title
		=> "Estragonia";

	public Control Create()
		=> new DemoView();

}
