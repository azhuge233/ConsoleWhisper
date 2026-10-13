using System;
using System.Collections.Generic;
using System.IO;

namespace ConsoleWhisper.Module {
	/// <summary>
	/// Expands one input pattern into matching file paths.
	/// Supported: literal paths, wildcards ('*', '?') in any segment, and '**' (any depth).
	/// Unreadable directories are skipped and reported through GlobResult.
	/// </summary>
	internal static class GlobExpander {
		private const string RecursiveSegment = "**";
		private const string AnyPattern = "*";

		internal static GlobResult Expand(string pattern) {
			var walker = new Walker();
			var (root, segments) = Split(pattern);

			if (segments.Count > 0)
				walker.Walk(root, segments, 0);

			return new GlobResult(walker.Files, walker.SkippedDirectories);
		}

		private static (string Root, IReadOnlyList<string> Segments) Split(string pattern) {
			var normalized = pattern.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
			var root = Path.GetPathRoot(normalized) ?? string.Empty;
			var remainder = normalized[root.Length..];
			var segments = remainder.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

			return (root.Length > 0 ? root : ".", segments);
		}

		private static bool HasWildcard(string segment) => segment.IndexOfAny(['*', '?']) >= 0;

		internal sealed record GlobResult(IReadOnlyList<string> Files, IReadOnlyList<string> SkippedDirectories);

		private sealed class Walker {
			internal List<string> Files { get; } = [];
			internal List<string> SkippedDirectories { get; } = [];

			internal void Walk(string currentDir, IReadOnlyList<string> segments, int index) {
				var segment = segments[index];
				var isLast = index == segments.Count - 1;

				if (segment == RecursiveSegment) {
					if (isLast) {
						foreach (var file in EnumerateFiles(currentDir, AnyPattern, recursive: true))
							Files.Add(file);
					} else {
						Walk(currentDir, segments, index + 1);

						foreach (var subDir in EnumerateDirectories(currentDir))
							Walk(subDir, segments, index);
					}
					return;
				}

				if (HasWildcard(segment)) {
					if (isLast) {
						foreach (var file in EnumerateFiles(currentDir, segment))
							Files.Add(file);
					} else {
						foreach (var subDir in EnumerateDirectories(currentDir, segment))
							Walk(subDir, segments, index + 1);
					}
					return;
				}

				var next = Path.Combine(currentDir, segment);
				if (isLast) {
					if (File.Exists(next))
						Files.Add(next);
				} else if (Directory.Exists(next)) {
					Walk(next, segments, index + 1);
				}
			}

			private IEnumerable<string> EnumerateFiles(string dir, string pattern, bool recursive = false) {
				var pending = new Stack<string>();
				pending.Push(dir);

				while (pending.Count > 0) {
					var current = pending.Pop();

					string[] files;
					try {
						files = Directory.GetFiles(current, pattern);
					} catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
						SkippedDirectories.Add(current);
						continue;
					}

					foreach (var file in files)
						yield return file;

					if (recursive)
						foreach (var subDir in EnumerateDirectories(current))
							pending.Push(subDir);
				}
			}

			private IEnumerable<string> EnumerateDirectories(string dir, string pattern = AnyPattern) {
				string[] subDirectories;
				try {
					subDirectories = Directory.GetDirectories(dir, pattern);
				} catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
					SkippedDirectories.Add(dir);
					yield break;
				}

				foreach (var subDir in subDirectories) {
					if (IsReparsePoint(subDir))
						continue;

					yield return subDir;
				}
			}

			private bool IsReparsePoint(string path) {
				try {
					return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
				} catch (Exception) {
					// Not readable as a directory - report it like the other skipped directories
					// instead of dropping it silently.
					SkippedDirectories.Add(path);
					return true;
				}
			}
		}
	}
}
