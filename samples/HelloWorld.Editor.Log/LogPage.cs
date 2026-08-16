using Avalonia.Controls;
using JLeb.Estragonia;

namespace HelloWorld.Editor.Log;

public sealed class LogPage : IEditorPage {

	public string Id
		=> "estragonia.log";

	public string Title
		=> "Estragonia Log";

	public Control Create()
		=> new LogView();

}
