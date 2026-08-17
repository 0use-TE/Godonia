using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GodotTools.ProjectEditor;

namespace GodotTools
{
    /// <summary>
    /// Copies the Godonia project template shipped next to this custom editor
    /// (<c>GodotSharp/Godonia/project-template</c>) into a new Godot project.
    /// Nested templates keep <c>project.godot</c> in the Godot folder and write the
    /// <c>.slnx</c> / CPM files one directory above so editor plugins can be siblings.
    /// </summary>
    internal static class GodoniaEditorBootstrap
    {
        private static readonly string[] TextExtensions =
        [
            ".cs", ".csproj", ".sln", ".slnx", ".axaml", ".godot", ".json", ".md", ".props",
            ".targets", ".config", ".editorconfig", ".tscn", ".cfg", ".svg"
        ];

        private static readonly string[] ParentFiles =
        [
            "Directory.Build.props",
            "Directory.Packages.props",
            "global.json",
            "README.md",
            "GodoniaApp.slnx"
        ];

        public static string? TemplateDirectory
        {
            get
            {
                string exe = Godot.OS.GetExecutablePath();
                if (string.IsNullOrEmpty(exe))
                    return null;

                string exeDir = Path.GetDirectoryName(exe)!;
                string godotSharp = Path.Combine(exeDir, "GodotSharp");
                foreach (string dir in new[]
                {
                    Path.Combine(godotSharp, "Godonia", "project-template"),
                    Path.Combine(godotSharp, "Tools", "project-template"),
                })
                {
                    if (IsNestedTemplate(dir) || File.Exists(Path.Combine(dir, "project.godot")))
                        return dir;
                }

                return null;
            }
        }

        public static string? NupkgDirectory
        {
            get
            {
                string exe = Godot.OS.GetExecutablePath();
                if (string.IsNullOrEmpty(exe))
                    return null;

                return Path.Combine(Path.GetDirectoryName(exe)!, "GodotSharp", "Godonia", "nupkg");
            }
        }

        public static bool TryInstall(string projectDir, string assemblyName)
        {
            string? template = TemplateDirectory;
            if (template is null)
                return false;

            string csName = IdentifierUtils.SanitizeQualifiedIdentifier(
                string.IsNullOrWhiteSpace(assemblyName) ? "GodotGame" : assemblyName,
                allowEmptyIdentifiers: true);
            if (string.IsNullOrEmpty(csName))
                csName = "GodotGame";

            bool nested = IsNestedTemplate(template);
            string godotSrc = nested ? Path.Combine(template, "GodotGame") : template;
            bool hadCsproj = Directory.EnumerateFiles(projectDir, "*.csproj").Any();
            bool isGodonia = File.Exists(Path.Combine(projectDir, "UI", "AvaloniaLoader.cs"))
                || File.Exists(Path.Combine(projectDir, "AvaloniaControl.cs"));

            // Existing non-Godonia C# project: do not write sibling Avalonia files into its parent folder.
            if (hadCsproj && !isGodonia)
                return true;

            string nupkg = (NupkgDirectory ?? "").Replace('\\', '/');

            if (!hadCsproj)
            {
                CopyDirectory(godotSrc, projectDir, skipProjectGodot: File.Exists(Path.Combine(projectDir, "project.godot")));
                string templateConfig = Path.Combine(projectDir, ".template.config");
                if (Directory.Exists(templateConfig))
                    Directory.Delete(templateConfig, recursive: true);

                foreach (string file in EnumerateFiles(projectDir))
                    ReplaceInFile(file, "GodotGame", csName, nupkg);

                TryRename(Path.Combine(projectDir, "GodotGame.csproj"), Path.Combine(projectDir, csName + ".csproj"));
            }

            if (nested)
            {
                string parent = Path.GetFullPath(Path.Combine(projectDir, ".."));
                string folderName = Path.GetFileName(projectDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                foreach (string name in ParentFiles)
                {
                    string destName = name == "GodoniaApp.slnx" ? csName + ".slnx" : name;
                    CopyParentFileIfMissing(Path.Combine(template, name), Path.Combine(parent, destName), csName, nupkg, folderName);
                }
                CopyEditorPreviewIfMissing(template, parent, csName, nupkg);
                WriteNugetConfig(parent, nupkg);
                RemoveLegacySlnIfSlxnExists(parent, csName);
                Godot.ProjectSettings.SetSetting("dotnet/project/solution_directory", "..");
                Godot.ProjectSettings.SetSetting("dotnet/project/assembly_name", csName);
            }
            else if (!hadCsproj)
            {
                TryRename(Path.Combine(projectDir, "GodoniaApp.slnx"), Path.Combine(projectDir, csName + ".slnx"));
                TryRename(Path.Combine(projectDir, "GodoniaApp.sln"), Path.Combine(projectDir, csName + ".slnx"));
                WriteNugetConfig(projectDir, nupkg);
            }

            if (!hadCsproj || File.Exists(Path.Combine(projectDir, "UI", "AvaloniaLoader.cs")))
                MergeProjectGodot(projectDir, csName, nested);

            return File.Exists(Path.Combine(projectDir, csName + ".csproj"))
                || Directory.EnumerateFiles(projectDir, "*.csproj").Any();
        }

        private static bool IsNestedTemplate(string template)
            => File.Exists(Path.Combine(template, "GodotGame", "project.godot"));

        private static void CopyParentFileIfMissing(string source, string dest, string csName, string nupkg, string? godotFolderName = null)
        {
            if (!File.Exists(source) || File.Exists(dest))
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(source, dest, overwrite: false);
            // Folder name may differ from assembly name (e.g. "zzz" vs "ZZZ").
            if (godotFolderName is not null && dest.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))
            {
                string text = File.ReadAllText(dest)
                    .Replace("GodotGame/GodotGame.csproj", $"{godotFolderName}/{csName}.csproj", StringComparison.Ordinal)
                    .Replace("GodotGame\\GodotGame.csproj", $"{godotFolderName}/{csName}.csproj", StringComparison.Ordinal)
                    .Replace("GodotGame.Editor.Preview/GodotGame.Editor.Preview.csproj", $"{csName}.Editor.Preview/{csName}.Editor.Preview.csproj", StringComparison.Ordinal)
                    .Replace("GodotGame.Editor.Preview\\GodotGame.Editor.Preview.csproj", $"{csName}.Editor.Preview/{csName}.Editor.Preview.csproj", StringComparison.Ordinal)
                    .Replace("__GODONIA_NUPKG__", nupkg, StringComparison.Ordinal);
                File.WriteAllText(dest, text);
            }
            else
            {
                ReplaceInFile(dest, "GodotGame", csName, nupkg);
            }
        }

        private static void CopyEditorPreviewIfMissing(string template, string parent, string csName, string nupkg)
        {
            string source = Path.Combine(template, "GodotGame.Editor.Preview");
            if (!Directory.Exists(source))
                return;

            string dest = Path.Combine(parent, csName + ".Editor.Preview");
            if (Directory.Exists(dest) && Directory.EnumerateFileSystemEntries(dest).Any())
                return;

            CopyDirectory(source, dest, skipProjectGodot: false);
            TryRename(Path.Combine(dest, "GodotGame.Editor.Preview.csproj"), Path.Combine(dest, csName + ".Editor.Preview.csproj"));
            foreach (string file in EnumerateFiles(dest))
                ReplaceInFile(file, "GodotGame", csName, nupkg);
        }

        private static void RemoveLegacySlnIfSlxnExists(string parent, string csName)
        {
            string slnx = Path.Combine(parent, csName + ".slnx");
            string sln = Path.Combine(parent, csName + ".sln");
            if (File.Exists(slnx) && File.Exists(sln))
                File.Delete(sln);
        }

        private static void WriteNugetConfig(string projectDir, string nupkg)
        {
            if (string.IsNullOrEmpty(nupkg))
                return;

            string path = Path.Combine(projectDir, "nuget.config");
            string nupkgUnix = nupkg.Replace('\\', '/');

            if (File.Exists(path))
            {
                string existing = File.ReadAllText(path);
                if (!existing.Contains("godonia-editor", StringComparison.OrdinalIgnoreCase))
                    return;
                if (!existing.Contains("globalPackagesFolder", StringComparison.OrdinalIgnoreCase))
                    return;
            }

            File.WriteAllText(path, $"""
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                  <packageSources>
                    <clear />
                    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
                    <add key="godonia-editor" value="{nupkgUnix}" />
                  </packageSources>
                  <packageSourceMapping>
                    <packageSource key="godonia-editor">
                      <package pattern="Ouse.Godonia" />
                      <package pattern="Ouse.Godonia.*" />
                    </packageSource>
                    <packageSource key="nuget.org">
                      <package pattern="*" />
                    </packageSource>
                  </packageSourceMapping>
                </configuration>
                """);
        }

        private static void CopyDirectory(string source, string dest, bool skipProjectGodot)
        {
            Directory.CreateDirectory(dest);
            foreach (string file in Directory.GetFiles(source))
            {
                string name = Path.GetFileName(file);
                if (name == ".template.config")
                    continue;
                if (skipProjectGodot && name == "project.godot")
                    continue;
                string destFile = Path.Combine(dest, name);
                if (!File.Exists(destFile))
                    File.Copy(file, destFile, overwrite: false);
            }

            foreach (string dir in Directory.GetDirectories(source))
            {
                string name = Path.GetFileName(dir);
                if (name is ".template.config" or ".git" or ".godot" or "bin" or "obj")
                    continue;
                CopyDirectory(dir, Path.Combine(dest, name), skipProjectGodot: false);
            }
        }

        private static void MergeProjectGodot(string projectDir, string csName, bool nested)
        {
            string path = Path.Combine(projectDir, "project.godot");
            if (!File.Exists(path))
                return;

            string text = File.ReadAllText(path);
            if (!text.Contains("AvaloniaLoader=", StringComparison.Ordinal))
            {
                if (!text.Contains("[autoload]", StringComparison.Ordinal))
                    text += "\n[autoload]\n\nAvaloniaLoader=\"res://UI/AvaloniaLoader.cs\"\n";
                else if (!text.Contains("AvaloniaLoader", StringComparison.Ordinal))
                    text = text.Replace("[autoload]", "[autoload]\n\nAvaloniaLoader=\"res://UI/AvaloniaLoader.cs\"", StringComparison.Ordinal);
            }

            const string plugin = "\"res://addons/godonia_new_plugin/plugin.cfg\"";
            if (!text.Contains(plugin, StringComparison.Ordinal))
            {
                if (!text.Contains("[editor_plugins]", StringComparison.Ordinal))
                    text += $"\n[editor_plugins]\n\nenabled=PackedStringArray({plugin})\n";
            }

            if (!text.Contains("[dotnet]", StringComparison.Ordinal))
            {
                text += $"\n[dotnet]\n\nproject/assembly_name=\"{csName}\"\n";
                if (nested)
                    text += "project/solution_directory=\"..\"\n";
            }
            else if (nested && !text.Contains("project/solution_directory", StringComparison.Ordinal))
            {
                text = text.Replace("[dotnet]", "[dotnet]\n\nproject/solution_directory=\"..\"", StringComparison.Ordinal);
            }

            if (text.Contains("PackedStringArray(\"4.7\"", StringComparison.Ordinal)
                && !text.Contains("\"C#\"", StringComparison.Ordinal))
                text = text.Replace("PackedStringArray(\"4.7\"", "PackedStringArray(\"4.7\", \"C#\"", StringComparison.Ordinal);

            File.WriteAllText(path, text);
        }

        private static IEnumerable<string> EnumerateFiles(string dir)
        {
            foreach (string file in Directory.GetFiles(dir))
                yield return file;
            foreach (string child in Directory.GetDirectories(dir))
            {
                string name = Path.GetFileName(child);
                if (name is ".git" or ".godot" or "bin" or "obj")
                    continue;
                foreach (string file in EnumerateFiles(child))
                    yield return file;
            }
        }

        private static void ReplaceInFile(string path, string from, string to, string nupkg)
        {
            string ext = Path.GetExtension(path);
            if (ext.Length == 0)
            {
                if (!string.Equals(Path.GetFileName(path), ".editorconfig", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(Path.GetFileName(path), "nuget.config", StringComparison.OrdinalIgnoreCase))
                    return;
            }
            else if (!TextExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            {
                return;
            }

            string text = File.ReadAllText(path);
            string updated = text.Replace(from, to, StringComparison.Ordinal)
                .Replace("__GODONIA_NUPKG__", nupkg, StringComparison.Ordinal);
            if (updated != text)
                File.WriteAllText(path, updated);
        }

        private static void TryRename(string from, string to)
        {
            if (File.Exists(from) && !File.Exists(to))
                File.Move(from, to);
        }
    }
}
