using ConsoleWhisper.Model;
using ConsoleWhisper.Module;

namespace ConsoleWhisper.Tests {
	[TestClass]
	public class ExpansionTests : TestDirectoryBase {
		[TestMethod]
		public void NonRecursiveWildcardMatchesTopLevelFilesOnly() {
			CreateFile("a.mp4");
			CreateFile("b.mp4");
			CreateFile("sub/c.mp4");

			var files = FileHelper.ExpandFilePaths([Path.Combine(TestRoot, "*.mp4")]);

			CollectionAssert.AreEqual(
				new[] { Path.Combine(TestRoot, "a.mp4"), Path.Combine(TestRoot, "b.mp4") },
				files);
		}

		[TestMethod]
		public void RecursiveWildcardWalksSubDirectories() {
			CreateFile("a.mp4");
			CreateFile("sub/c.mp4");
			CreateFile("sub/deep/d.mp4");

			var files = FileHelper.ExpandFilePaths([Path.Combine(TestRoot, "**", "*.mp4")]);

			Assert.HasCount(3, files);
			Assert.IsTrue(files.Exists(file => file.EndsWith("d.mp4")));
		}

		[TestMethod]
		public void QuestionMarkMatchesExactlyOneCharacter() {
			CreateFile("ab.mp4");
			CreateFile("ac.mp4");
			CreateFile("abc.mp4");

			var files = FileHelper.ExpandFilePaths([Path.Combine(TestRoot, "a?.mp4")]);

			CollectionAssert.AreEqual(
				new[] { Path.Combine(TestRoot, "ab.mp4"), Path.Combine(TestRoot, "ac.mp4") },
				files);
		}

		[TestMethod]
		public void DuplicateMatchesAreReturnedOnceAndSorted() {
			CreateFile("b.mp4");
			CreateFile("a.mp4");

			var files = FileHelper.ExpandFilePaths([Path.Combine(TestRoot, "*.mp4"), Path.Combine(TestRoot, "a.mp4")]);

			CollectionAssert.AreEqual(
				new[] { Path.Combine(TestRoot, "a.mp4"), Path.Combine(TestRoot, "b.mp4") },
				files);
		}

		[TestMethod]
		public void LiteralFileIsReturned() {
			var file = CreateFile("a.mp4");

			var files = FileHelper.ExpandFilePaths([file]);

			CollectionAssert.AreEqual(new[] { file }, files);
		}

		[TestMethod]
		public void NoMatchThrowsNoMatchException() {
			CreateFile("a.mp4");

			Assert.Throws<NoMatchException>(() => FileHelper.ExpandFilePaths([Path.Combine(TestRoot, "*.nomatch")]));
		}

		[TestMethod]
		public void NonExistingLiteralPathThrows() {
			Assert.Throws<NoMatchException>(() => FileHelper.ExpandFilePaths([Path.Combine(TestRoot, "nope.mp4")]));
		}

		[TestMethod]
		public void PartialMatchReturnsTheMatchingFiles() {
			CreateFile("a.mp4");

			var files = FileHelper.ExpandFilePaths([Path.Combine(TestRoot, "*.mp4"), Path.Combine(TestRoot, "*.nomatch")]);

			Assert.HasCount(1, files);
		}

		[TestMethod]
		public void SameFileReachedThroughDifferentPatternFormsIsReturnedOnce() {
			CreateFile("a.mp4");
			CreateFile("sub/c.mp4");

			var files = FileHelper.ExpandFilePaths([
				Path.Combine(TestRoot, "*.mp4"),
				Path.Combine(TestRoot, "sub", "..", "a.mp4")
			]);

			CollectionAssert.AreEqual(new[] { Path.Combine(TestRoot, "a.mp4") }, files);
		}

		[TestMethod]
		public void DirectoryAsInputThrowsWithHint() {
			CreateFile("sub/c.mp4");

			var exception = Assert.Throws<NoMatchException>(() => FileHelper.ExpandFilePaths([Path.Combine(TestRoot, "sub")]));

			StringAssert.Contains(exception.Message, "directory");
		}
	}
}
