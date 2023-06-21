using CommandLine;
using CommandLine.Text;
using ConsoleWhisper.Model;
using ConsoleWhisper.Module;
using System;
using System.Linq;

namespace ConsoleWhisper {
	internal class Program {
		static void Main(string[] args) {
			var parser = new Parser(options => options.HelpWriter = null);
			var parseResult = parser.ParseArguments<Argument>(args);

			parseResult.WithParsed(Run).WithNotParsed(errs => Error(parseResult));
		}

		static void Run(Argument arg) {
			Console.WriteLine(arg.Files.Count());
			var files = arg.Files.ToList();
			files.ForEach(Console.WriteLine);
			Output.Info($"{arg.ModelType}\n{arg.OutputDir}");
		}

		static void Error<T>(ParserResult<T> result) {
			var helpText = HelpText.AutoBuild(result, options => {
				options.MaximumDisplayWidth = Console.WindowWidth;
				options.AdditionalNewLineAfterOption = false;
				options.AutoHelp = true;
				return HelpText.DefaultParsingErrorsHandler(result, options);
			}, e => e);

			Output.Help(helpText);
		}
	}
}