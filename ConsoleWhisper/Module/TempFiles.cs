using System;
using System.Collections.Generic;
using System.IO;

namespace ConsoleWhisper.Module {
	/// <summary>
	/// Run level registry of temporary files, so every exit path (success, failure, cancellation)
	/// can clean them up.
	/// </summary>
	internal static class TempFiles {
		private static readonly object Lock = new();
		private static readonly List<string> RegisteredFiles = [];

		internal static void Register(string filePath) {
			lock (Lock)
				RegisteredFiles.Add(filePath);
		}

		internal static void Cleanup() {
			lock (Lock) {
				foreach (var filePath in RegisteredFiles) {
					try {
						if (File.Exists(filePath))
							File.Delete(filePath);
					} catch (Exception) {
						// best effort cleanup
					}
				}

				RegisteredFiles.Clear();
			}
		}
	}
}
