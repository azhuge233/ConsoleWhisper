using ConsoleWhisper.Module;

namespace ConsoleWhisper.Tests {
	[TestClass]
	public class NamingTests : TestDirectoryBase {
		[TestMethod]
		public void OutputBaseNameGetsSuffixOnCollision() {
			var outputDir = Path.Combine(TestRoot, "out");
			Directory.CreateDirectory(outputDir);

			var first = FileHelper.GetOutputBaseName(outputDir, "movie.mp4");
			var second = FileHelper.GetOutputBaseName(outputDir, "movie.mp4");

			Assert.AreEqual("movie-mp4", first);
			Assert.AreEqual("movie-mp4-2", second);
		}

		[TestMethod]
		public void OutputPathsUseTheOutputBaseName() {
			var outputDir = Path.Combine(TestRoot, "out");

			Assert.AreEqual(Path.Combine(outputDir, "movie-mp4.srt"), FileHelper.GetTranscriptPath(outputDir, "movie-mp4"));
			Assert.AreEqual(Path.Combine(outputDir, "movie-mp4.wav"), FileHelper.GetAudioPath(outputDir, "movie-mp4"));
		}

		[TestMethod]
		public void TempPathsKeepTheirTargetExtensionAndTmpSuffix() {
			var outputDir = Path.Combine(TestRoot, "out");

			StringAssert.EndsWith(FileHelper.GetTempTranscriptPath(outputDir, "movie-mp4"), "movie-mp4.srt.tmp");
			StringAssert.EndsWith(FileHelper.GetTempAudioPath(outputDir, "movie-mp4"), "movie-mp4.wav.tmp");
		}

		[TestMethod]
		public void OutputDirDefaultsToTheMediaDirectory() {
			var mediaPath = CreateFile("media/video.mp4");

			var resolved = FileHelper.ResolveOutputDir(null!, mediaPath);

			Assert.AreEqual(Path.GetDirectoryName(mediaPath), resolved);
		}

		[TestMethod]
		public void OutputDirIsCreatedWhenMissing() {
			var outputDir = Path.Combine(TestRoot, "new", "nested");

			var resolved = FileHelper.ResolveOutputDir(outputDir, "video.mp4");

			Assert.IsTrue(Directory.Exists(resolved));
			Assert.AreEqual(Path.GetFullPath(outputDir), resolved);
		}
	}
}
