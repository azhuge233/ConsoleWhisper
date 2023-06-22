using CommandLine;
using ConsoleWhisper.Model;
using ConsoleWhisper.Module;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace ConsoleWhisper {
	internal class Program {
		static async Task Main(string[] args) {
			var parseResult = new Parser(options => options.HelpWriter = null)
								.ParseArguments<Argument>(args);

			try {
				await parseResult.WithParsedAsync(Runner.Run);
				parseResult.WithNotParsed(errs => Runner.Error(parseResult, errs.ToList()));
			} catch (ArgumentException ex) {
				if (!string.IsNullOrEmpty(ex.Message)) Output.Error($"Argument Error: {ex.Message}");
				return;
			} catch (IOException ex) {
				if (!string.IsNullOrEmpty(ex.Message)) Output.Error($"IO Error: {ex.Message}");
				return;
			} catch (HttpRequestException ex) {
				if (!string.IsNullOrEmpty(ex.Message)) Output.Error($"Http Request Error: {ex.Message}");
				return;
			} catch (Exception ex) {
				if (!string.IsNullOrEmpty(ex.Message)) Output.Error($"Unknown Error: {ex.Message}");
				return;
			}
		}
	}
}