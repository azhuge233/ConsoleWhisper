using CommandLine;
using System;
using System.Collections.Generic;

namespace ConsoleWhisper.Model {
	public class Argument {
		[Option('i', "input", Required = true, Hidden = false, Separator = ' ', HelpText = "Input media files.")]
		[Value(2)]
		public IEnumerable<string> Files { get; set; }

		[Option('m', "model", Required = false, Hidden = false, Default = "small", HelpText = "Whiper model: base, tiny, small, medium, large")]
		[Value(0, Min = 0, Max = 1, Required = false)]
		public string ModelType { get; set; }

		[Option('o', "output", Required = false, Hidden = false, HelpText = "Output directory.")]
		[Value(1, Min = 0, Max = 1, Required = false)]
		public string OutputDir { get; set; }

		public Argument() {
			OutputDir = Environment.CurrentDirectory;
		}

		internal const int SupportedArgumentsCount = 5;
	}
}
