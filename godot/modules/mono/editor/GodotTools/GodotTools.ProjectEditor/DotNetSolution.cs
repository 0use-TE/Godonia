using GodotTools.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace GodotTools.ProjectEditor
{
    public class DotNetSolution
    {
        private readonly Dictionary<string, ProjectInfo> _projects = new Dictionary<string, ProjectInfo>();

        public string Name { get; }
        public string DirectoryPath { get; }

        public class ProjectInfo
        {
            public string Guid { get; }
            public string PathRelativeToSolution { get; }
            public List<string> Configs { get; }

            public ProjectInfo(string guid, string pathRelativeToSolution, List<string> configs)
            {
                Guid = guid;
                PathRelativeToSolution = pathRelativeToSolution;
                Configs = configs;
            }
        }

        public void AddNewProject(string name, ProjectInfo projectInfo)
        {
            _projects[name] = projectInfo;
        }

        public bool HasProject(string name)
        {
            return _projects.ContainsKey(name);
        }

        public ProjectInfo GetProjectInfo(string name)
        {
            return _projects[name];
        }

        public bool RemoveProject(string name)
        {
            return _projects.Remove(name);
        }

        public void Save()
        {
            if (!Directory.Exists(DirectoryPath))
                throw new FileNotFoundException("The solution directory does not exist.");

            var solution = new XElement("Solution");
            foreach (var pair in _projects)
            {
                string relativePath = pair.Value.PathRelativeToSolution.Replace('\\', '/');
                solution.Add(new XElement("Project", new XAttribute("Path", relativePath)));
            }

            string solutionPath = Path.Combine(DirectoryPath, Name + ".slnx");
            var doc = new XDocument(new XDeclaration("1.0", "utf-8", null), solution);

            var settings = new System.Xml.XmlWriterSettings
            {
                Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                Indent = true,
                OmitXmlDeclaration = true,
                NewLineChars = "\n",
                NewLineHandling = System.Xml.NewLineHandling.Replace
            };

            using (var writer = System.Xml.XmlWriter.Create(solutionPath, settings))
                doc.Save(writer);
        }

        public DotNetSolution(string name, string directoryPath)
        {
            Name = name;
            DirectoryPath = directoryPath.IsAbsolutePath() ? directoryPath : Path.GetFullPath(directoryPath);
        }

        public static void MigrateFromOldConfigNames(string slnPath)
        {
            if (!File.Exists(slnPath))
                return;

            // Legacy .sln only — .slnx does not use these configuration monikers.
            if (!slnPath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
                return;

            string input = File.ReadAllText(slnPath);

            if (!Regex.IsMatch(input, Regex.Escape("Tools|Any CPU")))
                return;

            // This method renames old configurations in solutions to the new ones.
            //
            // This is the order configs appear in the solution and what we want to rename them to:
            //   Debug|Any CPU = Debug|Any CPU        ->    ExportDebug|Any CPU = ExportDebug|Any CPU
            //   Tools|Any CPU = Tools|Any CPU        ->    Debug|Any CPU = Debug|Any CPU
            //
            // But we want to move Tools (now Debug) to the top, so it's easier to rename like this:
            //   Debug|Any CPU = Debug|Any CPU        ->    Debug|Any CPU = Debug|Any CPU
            //   Release|Any CPU = Release|Any CPU    ->    ExportDebug|Any CPU = ExportDebug|Any CPU
            //   Tools|Any CPU = Tools|Any CPU        ->    ExportRelease|Any CPU = ExportRelease|Any CPU

            var dict = new Dictionary<string, string>
            {
                {"Debug|Any CPU", "Debug|Any CPU"},
                {"Release|Any CPU", "ExportDebug|Any CPU"},
                {"Tools|Any CPU", "ExportRelease|Any CPU"}
            };

            var regex = new Regex(string.Join("|", dict.Keys.Select(Regex.Escape)));
            string result = regex.Replace(input, m => dict[m.Value]);

            if (result != input)
            {
                // Save a copy of the solution before replacing it
                FileUtils.SaveBackupCopy(slnPath);

                File.WriteAllText(slnPath, result);
            }
        }
    }
}
