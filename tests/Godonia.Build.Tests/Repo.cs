using System;
using System.IO;

namespace Godonia.Build.Tests;

internal static class Repo {

	public static string Root {
		get {
			var dir = new DirectoryInfo(AppContext.BaseDirectory);
			while (dir is not null) {
				if (File.Exists(Path.Combine(dir.FullName, "Ouse.Godonia.slnx"))
					|| File.Exists(Path.Combine(dir.FullName, "Ouse.Godonia.sln")))
					return dir.FullName;
				dir = dir.Parent;
			}

			throw new InvalidOperationException("Could not find Ouse.Godonia.slnx from " + AppContext.BaseDirectory);
		}
	}

	public static string HostScriptsDir
		=> Path.Combine(Root, "src", "Ouse.Godonia", "host-scripts");

}
