using System;
using System.Collections.Generic;
using System.Globalization;

namespace ConsoleWhisper.Module {
	/// <summary>
	/// Matches a requested audio language code against the language tag reported for an audio stream.
	/// Accepts both ISO 639-1 (e.g. "en") and the tag exactly as reported (usually ISO 639-2, e.g. "eng").
	/// The 2-letter to 3-letter mapping comes from CultureInfo, so no hand-maintained table is needed.
	/// </summary>
	internal static class AudioLanguage {
		internal static bool Matches(string requestedCode, string streamLanguage) {
			var requested = Normalize(requestedCode);
			var reported = Normalize(streamLanguage);

			if (requested.Length == 0 || reported.Length == 0)
				return false;

			if (string.Equals(requested, reported, StringComparison.OrdinalIgnoreCase))
				return true;

			return IsTwoLetterFormOf(requested, reported) || IsTwoLetterFormOf(reported, requested);
		}

		/// <summary>
		/// Returns the index of the first stream whose language matches the requested code, or -1 when none matches.
		/// </summary>
		internal static int FindMatch(string requestedCode, IReadOnlyList<string> streamLanguages) {
			for (var index = 0; index < streamLanguages.Count; index++)
				if (Matches(requestedCode, streamLanguages[index]))
					return index;

			return -1;
		}

		private static bool IsTwoLetterFormOf(string twoLetterCode, string threeLetterCode) {
			if (twoLetterCode.Length != 2 || threeLetterCode.Length != 3)
				return false;

			return ThreeLetterByTwoLetter.Value.TryGetValue(twoLetterCode, out var mapped)
				&& string.Equals(mapped, threeLetterCode, StringComparison.OrdinalIgnoreCase);
		}

		private static string Normalize(string languageCode) {
			var code = (languageCode ?? string.Empty).Trim().ToLowerInvariant();
			var subTagIndex = code.IndexOfAny(['-', '_']);

			if (subTagIndex > 0)
				code = code[..subTagIndex];

			// ISO 639-2/B (bibliographic - what media containers usually carry) to 639-2/T (what .NET uses),
			// so "chi" / "ger" tags match a "zh" / "de" request and the other way round.
			return BibliographicToTerminological.TryGetValue(code, out var terminological) ? terminological : code;
		}

		private static readonly Dictionary<string, string> BibliographicToTerminological = new(StringComparer.OrdinalIgnoreCase) {
			["alb"] = "sqi", ["arm"] = "hye", ["baq"] = "eus", ["bur"] = "mya", ["chi"] = "zho", ["cze"] = "ces",
			["dut"] = "nld", ["fre"] = "fra", ["geo"] = "kat", ["ger"] = "deu", ["gre"] = "ell", ["ice"] = "isl",
			["mac"] = "mkd", ["mao"] = "mri", ["may"] = "msa", ["per"] = "fas", ["rum"] = "ron", ["slo"] = "slk",
			["tib"] = "bod", ["wel"] = "cym"
		};

		private static readonly Lazy<Dictionary<string, string>> ThreeLetterByTwoLetter = new(BuildThreeLetterByTwoLetter);

		private static Dictionary<string, string> BuildThreeLetterByTwoLetter() {
			var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

			foreach (var culture in CultureInfo.GetCultures(CultureTypes.AllCultures)) {
				var twoLetter = culture.TwoLetterISOLanguageName;
				var threeLetter = culture.ThreeLetterISOLanguageName;

				if (twoLetter.Length == 2 && threeLetter.Length == 3 && !string.Equals(twoLetter, threeLetter, StringComparison.OrdinalIgnoreCase))
					map.TryAdd(twoLetter, threeLetter);
			}

			return map;
		}
	}
}
