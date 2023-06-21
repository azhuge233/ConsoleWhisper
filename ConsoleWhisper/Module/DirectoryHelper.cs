using System;
using System.Collections.Generic;
using System.IO;

namespace ConsoleWhisper.Module {
	internal static class DirectoryHelper {
		internal static IEnumerable<string> ExpandFilePaths(IEnumerable<string> paths) {
			try {
				var fileList = new List<string>();

				foreach (var path in paths) {
					var substitutedArg = System.Environment.ExpandEnvironmentVariables(path);

					var dirPart = Path.GetDirectoryName(substitutedArg);
					if (dirPart.Length == 0)
						dirPart = ".";

					var filePart = Path.GetFileName(substitutedArg);

					foreach (var filepath in Directory.GetFiles(dirPart, filePart))
						fileList.Add(filepath);
				}

				return fileList;
			} catch (Exception) {
				throw;
			}
		}

		internal static string GetTempWavFile() {
			string tempFilename = Path.GetTempFileName();
			string waveFilename = Path.ChangeExtension(tempFilename, WaveExtension);
			File.Delete(tempFilename);
			return waveFilename;
		}

		private const string WaveExtension = "wav";
	}
}
