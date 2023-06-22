using ConsoleWhisper.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
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

		internal static IEnumerable<string> ExpandFilePaths(IEnumerable<string> paths) {
			try {
				var fileList = new List<string>();

				foreach (var path in paths) {
					var substitutedArg = Environment.ExpandEnvironmentVariables(path);

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

		internal static void DelFile(string filePath) {
			File.Delete(filePath);
		}

		internal static string GetTranscriptPath(string outputDir, string filename) {
			var extension = Path.GetExtension(filename).TrimStart('.');
			var filenameWithoutExtension = Path.GetFileNameWithoutExtension(filename);
			var transcriptName = Path.ChangeExtension($"{filenameWithoutExtension}-{extension}.tmp", SrtExtension);
			return Path.Combine(outputDir, transcriptName);
		}

		internal static string GetAudioPath(string outputDir, string filename) {
			var extension = Path.GetExtension(filename).TrimStart('.');
			var filenameWithoutExtension = Path.GetFileNameWithoutExtension(filename);
			var audioName = Path.ChangeExtension($"{filenameWithoutExtension}-{extension}.tmp", Mp3Extension);
			return Path.Combine(outputDir, audioName);
		}

		internal static async Task DownloadFFmpegandModel(Argument arg) {
			try {
				FFmpeg.SetExecutablesPath(AppDirectory);

				var tasks = new List<Task>() { 
					Task.Run(DownloadFFmpeg),
					Task.Run(() => DownloadModel(arg.ModelType))
				};
				
				await Task.WhenAll(tasks);
			} catch (Exception) {
				throw;
			}
		}

		#region Get temp file name
		internal static string GetTempFile() {
			return Path.GetTempFileName();
		}

		internal static string GetTempWavFile() {
			string tempFilename = GetTempFile();
			string waveFilename = Path.ChangeExtension(tempFilename, WaveExtension);
			DelFile(tempFilename);
			return waveFilename;
		}

		internal static string GetTempAacFile() {
			string tempFilename = GetTempFile();
			string waveFilename = Path.ChangeExtension(tempFilename, AacExtension);
			DelFile(tempFilename);
			return waveFilename;
		}

		internal static string GetTempMp3File() {
			string tempFilename = GetTempFile();
			string waveFilename = Path.ChangeExtension(tempFilename, Mp3Extension);
			DelFile(tempFilename);
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
			byte[] info = encoder.GetBytes(value.ToString("G"));
			await fs.WriteAsync(info);
		}
		#endregion

		private static readonly UTF8Encoding encoder = new(true);

		#region Extension string
		private const string WaveExtension = "wav";
		private const string AacExtension = "aac";
		private const string Mp3Extension = "mp3";
		private const string SrtExtension = "srt";
		#endregion

		#region downloader
		private static async Task DownloadFFmpeg() {
			if (!FFmpegExists()) {
				Output.Warn($"FFmpeg not found, start downloading.");
				await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official);
				File.Delete("version.json");

				if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
					File.Move("ffmpeg.exe", FFmpegLocationWindows);
					File.Move("ffprobe.exe", FFprobeLocationWindows);
				} else {
					File.Move("ffmpeg", FFmpegLocationUnix);
					File.Move("ffprobe", FFprobeLocationUnix);
				}
			}
		}

		private static async Task DownloadModel(string ModelType) {
			if (!ModelExists(ModelType)) {
				Output.Warn($"{ModelType} is not found in Models directory, start downloading.");
				await WhisperHelper.DownloadModel(ModelType);
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
