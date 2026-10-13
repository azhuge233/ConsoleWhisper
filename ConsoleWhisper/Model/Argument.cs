using CommandLine;
using System;
using System.Collections.Generic;

namespace ConsoleWhisper.Model {
	public class Argument {
		[Option('i', "input", Required = true, Hidden = false, Separator = ',', HelpText = "Input media files.")]
		[Value(5)]
		public IEnumerable<string> Files { get; set; }

		[Option('m', "model", Required = false, Hidden = false, Default = "small", HelpText = "Whisper model: base, tiny, small, medium, large-v1, large-v2, large-v3, large-v3-turbo.")]
		[Value(0, Min = 0, Max = 1, Required = false)]
		public string ModelType { get; set; }

		[Option('o', "output", Required = false, Hidden = false, HelpText = "(Default: media file's directory) Output directory.")]
		[Value(1, Min = 0, Max = 1, Required = false)]
		public string OutputDir { get; set; }

		[Option('l', "language", Required = false, Hidden = false, Default = "auto", HelpText = "Specify transcribe language.")]
		[Value(2, Required = false)]
		public string Language { get; set; }

		[Option("only-extract", Required = false, Hidden = false, Default = false, HelpText = "Extract audio stream only.")]
		[Value(3, Required = false)]
		public bool OnlyExtract { get; set; }

		[Option("multithread", Required = false, Hidden = false, Default = false, HelpText = "Use multithread, only works for audio extraction.")]
		[Value(4, Required = false)]
		public bool Multithread { get; set; }

		[Option("audio-index", Required = false, Hidden = false, HelpText = "Audio stream index (0-based) to use, for media files with multiple audio streams.")]
		public int? AudioIndex { get; set; }

		[Option("audio-lang", Required = false, Hidden = false, HelpText = "Audio stream language to use (e.g. eng or en), for media files with multiple audio streams.")]
		public string AudioLang { get; set; }

		[Option('y', "yes", Required = false, Hidden = false, Default = false, HelpText = "Answer yes to confirmation prompts (non-interactive use).")]
		public bool Yes { get; set; }

		[Option("verbose", Required = false, Hidden = false, Default = false, HelpText = "Print full exception details (stack trace) on errors.")]
		public bool Verbose { get; set; }

		//[Option('g', "gpu", Required = false, Hidden = true, HelpText = "Currently not implemented.")]
		//[Value(2, Required = false, Default = false)]
		//public bool GPU { get; set; }

		public void Validate() {
			if (!SupportedModels.Contains(ModelType))
				throw new ArgumentException(message: $"Whisper model type \"{ModelType}\" is not supported.");
			if (!SupportedLanguages.Contains(Language))
				throw new ArgumentException(message: $"Language \"{Language}\" is not supported.\nCheck {LanguageLink} for available languages.");
			if (AudioIndex < 0)
				throw new ArgumentException(message: "Audio stream index must be 0 or greater.");
			if (AudioIndex.HasValue && !string.IsNullOrWhiteSpace(AudioLang))
				throw new ArgumentException(message: "Specify either --audio-index or --audio-lang, not both.");
		}

		private static readonly HashSet<string> SupportedModels = [ "base", "tiny", "small", "medium", "large-v1", "large-v2", "large-v3", "large-v3-turbo" ];
		private static readonly HashSet<string> SupportedLanguages = [ "en", "zh", "de", "es", "ru", "ko", "fr", "ja", "pt", "tr",
			"pl", "ca", "nl", "ar", "sv", "it", "id", "hi", "fi", "vi", "he", "uk", "el", "ms", "ro", "da", "hu", "ta", "no", "th", "ur",
			"hr", "bg", "lt", "la", "mi", "ml", "cy", "sk", "te", "fa", "lv", "bn", "sr", "az", "sl", "kn", "et", "mk", "br", "eu", "is",
			"hy", "ne", "mn", "bs", "kk", "sq", "sw", "gl", "mr", "pa", "si", "km", "sn", "yo", "so", "af", "oc", "ka", "be", "tg", "sd",
			"gu", "am", "yi", "lo", "uz", "fo", "ht", "ps", "tk", "nn", "mt", "sa", "lb", "my", "bo", "tl", "mg", "as", "tt", "haw", "ln",
			"ha", "ba", "jw", "su", "auto" ];

		private const string LanguageLink = "https://github.com/ggerganov/whisper.cpp/blob/57543c169e27312e7546d07ed0d8c6eb806ebc36/whisper.cpp#L121";
	}
}
