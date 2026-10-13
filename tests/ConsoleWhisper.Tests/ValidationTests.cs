using ConsoleWhisper.Model;
using ConsoleWhisper.Module;

namespace ConsoleWhisper.Tests {
	[TestClass]
	public class ValidationTests {
		[TestMethod]
		public void ValidateAcceptsDefaults() {
			var argument = new Argument { ModelType = "small", Language = "auto" };

			argument.Validate();
		}

		[TestMethod]
		public void ValidateRejectsUnknownModel() {
			var argument = new Argument { ModelType = "nope", Language = "auto" };

			Assert.Throws<ArgumentException>(argument.Validate);
		}

		[TestMethod]
		public void ValidateRejectsUnknownLanguage() {
			var argument = new Argument { ModelType = "small", Language = "klingon" };

			Assert.Throws<ArgumentException>(argument.Validate);
		}

		[TestMethod]
		public void ValidateRejectsNegativeAudioIndex() {
			var argument = new Argument { ModelType = "small", Language = "auto", AudioIndex = -1 };

			Assert.Throws<ArgumentException>(argument.Validate);
		}

		[TestMethod]
		public void ValidateRejectsAudioIndexAndLanguageTogether() {
			var argument = new Argument { ModelType = "small", Language = "auto", AudioIndex = 0, AudioLang = "eng" };

			Assert.Throws<ArgumentException>(argument.Validate);
		}

		[TestMethod]
		public void LanguageMatchesTwoAndThreeLetterCodes() {
			Assert.IsTrue(AudioLanguage.Matches("en", "eng"));
			Assert.IsTrue(AudioLanguage.Matches("ENG", "en"));
			Assert.IsTrue(AudioLanguage.Matches("ja", "jpn"));
			Assert.IsTrue(AudioLanguage.Matches("zh", "zho"));
			Assert.IsTrue(AudioLanguage.Matches("en-US", "eng"));
		}

		[TestMethod]
		public void FindMatchReturnsTheFirstMatchingStreamIndex() {
			Assert.AreEqual(1, AudioLanguage.FindMatch("eng", ["jpn", "eng"]));
			Assert.AreEqual(1, AudioLanguage.FindMatch("en", ["jpn", "eng"]));
			Assert.AreEqual(-1, AudioLanguage.FindMatch("eng", ["und", "jpn"]));
			Assert.AreEqual(-1, AudioLanguage.FindMatch("eng", []));
		}

		[TestMethod]
		public void LanguageMatchesBibliographicThreeLetterCodes() {
			Assert.IsTrue(AudioLanguage.Matches("zh", "chi"));
			Assert.IsTrue(AudioLanguage.Matches("de", "ger"));
			Assert.IsTrue(AudioLanguage.Matches("chi", "zh"));
			Assert.IsTrue(AudioLanguage.Matches("ger", "de"));
			Assert.IsFalse(AudioLanguage.Matches("de", "dut"));
		}

		[TestMethod]
		public void LanguageDoesNotMatchUnrelatedOrEmptyCodes() {
			Assert.IsFalse(AudioLanguage.Matches("en", "jpn"));
			Assert.IsFalse(AudioLanguage.Matches("", "eng"));
			Assert.IsFalse(AudioLanguage.Matches("eng", ""));
		}
	}
}
