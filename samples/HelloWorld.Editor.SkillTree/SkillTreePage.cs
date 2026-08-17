using Avalonia.Controls;
using Ouse.Godonia;

namespace HelloWorld.Editor.SkillTree;

public sealed class SkillTreePage : IEditorPage {

	public string Id => "helloworld.skill.tree";

	public string Title => "Skill Tree";

	public Control Create() => new SkillTreeView();

}