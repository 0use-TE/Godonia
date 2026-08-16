using System;
using System.IO;

namespace Estragonia.Build.Tests;

internal static class Repo {

	public static string Root {
		get {
			var dir = new DirectoryInfo(AppContext.BaseDirectory);
			while (dir is not null) {
				if (File.Exists(Path.Combine(dir.FullName, "JLeb.Estragonia.sln")))
					return dir.FullName;
				dir = dir.Parent;
			}

			throw new InvalidOperationException("Could not find JLeb.Estragonia.sln from " + AppContext.BaseDirectory);
		}
	}

	public static string HostScriptsDir
		=> Path.Combine(Root, "src", "JLeb.Estragonia", "host-scripts");

}
