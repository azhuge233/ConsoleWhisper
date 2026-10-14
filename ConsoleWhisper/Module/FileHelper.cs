using ConsoleWhisper.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;

namespace ConsoleWhisper.Module {
	internal static class FileHelper {
		#region Directories
		internal static readonly string AppDirectory = AppDomain.CurrentDomain.BaseDirectory;
		internal static readonly string FFmpegLocationWindows = Path.Combine(AppDirectory, "ffmpeg.exe");
		internal static readonly string FFprobeLocationWindows = Path.Combine(AppDirectory, "ffprobe.exe");
		internal static readonly string FFmpegLocationUnix = Path.Combine(AppDirectory, "ffmpeg");
		internal static readonly string FFprobeLocationUnix = Path.Combine(AppDirectory, "ffprobe");
		internal static readonly string ModelDirectory = Path.Combine(AppDirectory, "Model");
		#endregion

		internal static List<string> ExpandFilePaths(IEnumerable<string> patterns) {
			var results = new List<string>();
			var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var misses = new List<string>();
			var skippedDirectories = new List<string>();

			foreach (var rawPattern in patterns) {
				var pattern = Environment.ExpandEnvironmentVariables(rawPattern);
				var expansion = GlobExpander.Expand(pattern);

				if (expansion.Files.Count == 0)
					misses.Add(pattern);

				foreach (var match in expansion.Files) {
					// Deduplicate on the full path: the same file can come back as ".\a.mp4" from a
					// relative pattern and as "D:\videos\a.mp4" from an absolute one.
					var file = Path.GetFullPath(match);

					if (seen.Add(file))
						results.Add(file);
				}

				skippedDirectories.AddRange(expansion.SkippedDirectories);
			}

			if (skippedDirectories.Count > 0) {
				var label = skippedDirectories.Count == 1 ? "directory" : "directories";
				Output.Warn($"{skippedDirectories.Count} unreadable {label} skipped: {string.Join(", ", skippedDirectories.Distinct())}");
			}

			if (results.Count == 0)
				throw new NoMatchException(BuildNoMatchMessage(misses));

			foreach (var miss in misses)
				Output.Warn($"Input \"{miss}\" matched no files.");

			results.Sort(StringComparer.OrdinalIgnoreCase);
			return results;
		}

		private static string BuildNoMatchMessage(List<string> misses) {
			var lines = new List<string> { "No input files matched:" };

			foreach (var miss in misses) {
				var hint = Directory.Exists(miss)
					? $" (this is a directory - use a wildcard, e.g. \"{Path.Combine(miss, "*.mp4")}\" or \"{Path.Combine(miss, "**", "*.mp4")}\")"
					: " (file not found)";
				lines.Add($"  - \"{miss}\"{hint}");
			}

			return string.Join(Environment.NewLine, lines);
		}

		internal static string ResolveOutputDir(string? outputDir, string mediaFilename) {
			var dir = string.IsNullOrWhiteSpace(outputDir)
				? Path.GetDirectoryName(Path.GetFullPath(mediaFilename))
				: Path.GetFullPath(outputDir);

			if (string.IsNullOrEmpty(dir))
				throw new ArgumentException(message: $"Cannot determine an output directory for \"{mediaFilename}\".");

			Directory.CreateDirectory(dir);
			return dir;
		}

		internal static void DelFile(string? filePath) {
			// Cleanup helper: a missing path (for example a failed extraction) is not an error.
			if (filePath is null)
				return;

			File.Delete(filePath);
		}

		internal static string GetOutputBaseName(string outputDir, string mediaFilename) {
			var extension = Path.GetExtension(mediaFilename).TrimStart('.');
			var filenameWithoutExtension = Path.GetFileNameWithoutExtension(mediaFilename);
			var baseName = $"{filenameWithoutExtension}-{extension}";

			lock (UsedOutputBaseNamesLock) {
				var candidate = baseName;
				var suffix = 2;

				while (!UsedOutputBaseNames.Add(Path.Combine(outputDir, candidate)))
					candidate = $"{baseName}-{suffix++}";

				return candidate;
			}
		}

		internal static string GetTranscriptPath(string outputDir, string outputBaseName) {
			return Path.Combine(outputDir, $"{outputBaseName}.{SrtExtension}");
		}

		internal static string GetAudioPath(string outputDir, string outputBaseName) {
			return Path.Combine(outputDir, $"{outputBaseName}.{WaveExtension}");
		}

		internal static string GetTempAudioPath(string outputDir, string outputBaseName) {
			var tempPath = Path.Combine(outputDir, $"{outputBaseName}.{WaveExtension}.tmp");
			TempFiles.Register(tempPath);
			return tempPath;
		}

		internal static string GetTempTranscriptPath(string outputDir, string outputBaseName) {
			var tempPath = Path.Combine(outputDir, $"{outputBaseName}.{SrtExtension}.tmp");
			TempFiles.Register(tempPath);
			return tempPath;
		}

		#region Get temp file name
		internal static string GetTempFile() {
			return Path.GetTempFileName();
		}

		internal static string GetTempWavFile() {
			string tempFilename = GetTempFile();
			string waveFilename = Path.ChangeExtension(tempFilename, WaveExtension);
			DelFile(tempFilename);
			TempFiles.Register(waveFilename);
			return waveFilename;
		}

		#endregion

		#region Whisper model related

		internal static string GetModelName(string modelType) {
			return $"ggml-{modelType}.bin";
		}

		internal static string GetModelPath(string modelType) {
			return Path.Combine(ModelDirectory, modelType);
		}

		internal static string GetTempModelPath(string modelPath) {
			var tempPath = $"{modelPath}.tmp";
			TempFiles.Register(tempPath);
			return tempPath;
		}
		#endregion

		#region Add Text to Filestream
		internal static async Task AddText(FileStream fs, string value) {
			byte[] info = encoder.GetBytes(value);
			await fs.WriteAsync(info);
		}

		internal static async Task AddText(FileStream fs, int value) {
			byte[] info = encoder.GetBytes($"{value}\n");
			await fs.WriteAsync(info);
		}

		internal static async Task AddText(FileStream fs, TimeSpan value) {
			byte[] info = encoder.GetBytes(value.ToString(@"hh\:mm\:ss\,fff"));
			await fs.WriteAsync(info);
		}
		#endregion

		private static readonly UTF8Encoding encoder = new(true);
		private static readonly object UsedOutputBaseNamesLock = new();
		private static readonly HashSet<string> UsedOutputBaseNames = new(StringComparer.OrdinalIgnoreCase);

		#region Extension string
		private const string WaveExtension = "wav";
		private const string SrtExtension = "srt";
		#endregion

		#region downloader
		internal static async Task PrepareFFmpeg() {
			FFmpeg.SetExecutablesPath(AppDirectory);

			if (FFmpegExists())
				return;

			Output.Warn("FFmpeg not found, start downloading.");
			await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, AppDirectory, new Progress<ProgressInfo>(progress => Output.ReportDownload("FFmpeg", progress.DownloadedBytes, progress.TotalBytes)));
			Output.CompleteDownload("FFmpeg");
		}

		internal static async Task PrepareModel(string modelType, CancellationToken cancellationToken) {
			RemoveStaleModelFiles();

			if (ModelExists(modelType))
				return;

			Output.Warn($"Whisper model \"{GetFriendlyModelName(modelType)}\" is not found, start downloading.");
			await WhisperHelper.DownloadModel(modelType, cancellationToken);
		}

		internal static string GetFriendlyModelName(string modelType) {
			var name = modelType;

			if (name.StartsWith("ggml-", StringComparison.OrdinalIgnoreCase))
				name = name["ggml-".Length..];

			if (name.EndsWith(".bin", StringComparison.OrdinalIgnoreCase))
				name = name[..^4];

			return name;
		}

		// A hard killed process can leave "<model>.bin.tmp" behind - TempFiles only lives for one run.
		private static void RemoveStaleModelFiles() {
			if (!Directory.Exists(ModelDirectory))
				return;

			foreach (var staleFile in Directory.GetFiles(ModelDirectory, "*.tmp")) {
				try {
					DelFile(staleFile);
				} catch (Exception) {
					// best effort cleanup - leave the file when it is locked
				}
			}
		}
		#endregion

		#region Check if necessary file exists
		private static bool ModelExists(string modelFilename) {
			return File.Exists(Path.Combine(ModelDirectory, modelFilename));
		}

		private static bool FFmpegExists() {
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				return File.Exists(FFmpegLocationWindows) && File.Exists(FFprobeLocationWindows);
			else 
				return File.Exists(FFmpegLocationUnix) && File.Exists(FFprobeLocationUnix);
		}
		#endregion
	}
}
