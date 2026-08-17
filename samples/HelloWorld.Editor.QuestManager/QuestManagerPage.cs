using Avalonia.Controls;
using Ouse.Godonia;

namespace HelloWorld.Editor.QuestManager;

public sealed class QuestManagerPage : IEditorPage {

	public string Id => "helloworld.quest.manager";

	public string Title => "Quest Manager";

	public Control Create() => new QuestManagerView();

}