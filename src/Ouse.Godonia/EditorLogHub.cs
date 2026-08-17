using System;
using System.Collections.Generic;

namespace Ouse.Godonia;

/// <summary>
/// Process-wide log buffer for editor plugins. Plugins must not subscribe to static events
/// on their own types; read <see cref="Snapshot"/> instead so Reload can collect the ALC.
/// </summary>
public static class EditorLogHub {

	private const int MaxLines = 200;
	private static readonly object s_lock = new();
	private static readonly List<string> s_lines = [];

	public static void Write(string message) {
		var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
		lock (s_lock) {
			s_lines.Add(line);
			if (s_lines.Count > MaxLines)
				s_lines.RemoveRange(0, s_lines.Count - MaxLines);
		}
	}

	public static IReadOnlyList<string> Snapshot() {
		lock (s_lock)
			return s_lines.ToArray();
	}

	public static void Clear() {
		lock (s_lock)
			s_lines.Clear();
	}

}
