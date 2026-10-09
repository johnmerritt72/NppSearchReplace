using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NppSearchReplace.Core.Tests
{
    [TestClass]
    public class SearchHistoryTests
    {
        [TestMethod]
        public void PushSearch_InsertsMostRecentFirst()
        {
            var h = new SearchHistory();
            h.PushSearch("one");
            h.PushSearch("two");
            CollectionAssert.AreEqual(new[] { "two", "one" }, h.SearchTexts);
        }

        [TestMethod]
        public void PushSearch_ExistingEntry_MovesToFrontWithoutDuplicate()
        {
            var h = new SearchHistory();
            h.PushSearch("one");
            h.PushSearch("two");
            h.PushSearch("one");
            CollectionAssert.AreEqual(new[] { "one", "two" }, h.SearchTexts);
        }

        [TestMethod]
        public void PushSearch_IsCaseSensitiveAndWhitespaceSensitive()
        {
            var h = new SearchHistory();
            h.PushSearch("One");
            h.PushSearch("one");
            h.PushSearch("one ");
            Assert.AreEqual(3, h.SearchTexts.Count);
        }

        [TestMethod]
        public void PushSearch_EmptyOrNull_IsIgnored()
        {
            var h = new SearchHistory();
            h.PushSearch("");
            h.PushSearch(null);
            Assert.AreEqual(0, h.SearchTexts.Count);
        }

        [TestMethod]
        public void PushSearch_TrimsToMaxEntries()
        {
            var h = new SearchHistory();
            for (int i = 0; i < SearchHistory.MaxEntries + 5; i++)
                h.PushSearch("entry" + i);
            Assert.AreEqual(SearchHistory.MaxEntries, h.SearchTexts.Count);
            Assert.AreEqual("entry" + (SearchHistory.MaxEntries + 4), h.SearchTexts[0]);
        }

        [TestMethod]
        public void PushReplace_IsIndependentOfSearch()
        {
            var h = new SearchHistory();
            h.PushSearch("s");
            h.PushReplace("r");
            CollectionAssert.AreEqual(new[] { "s" }, h.SearchTexts);
            CollectionAssert.AreEqual(new[] { "r" }, h.ReplaceTexts);
        }

        [TestMethod]
        public void SaveAndLoad_RoundTripsMultilineTextAndOrder()
        {
            string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".xml");
            try
            {
                var h = new SearchHistory();
                h.PushSearch("line1\r\nline2");
                h.PushSearch("  leading and trailing  ");
                h.PushReplace("<xml & stuff>");
                h.Save(path);

                var loaded = SearchHistory.Load(path);
                CollectionAssert.AreEqual(new[] { "  leading and trailing  ", "line1\r\nline2" }, loaded.SearchTexts);
                CollectionAssert.AreEqual(new[] { "<xml & stuff>" }, loaded.ReplaceTexts);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void Load_MissingFile_ReturnsEmptyHistory()
        {
            var loaded = SearchHistory.Load(Path.Combine(Path.GetTempPath(), "does-not-exist-" + Path.GetRandomFileName()));
            Assert.AreEqual(0, loaded.SearchTexts.Count);
            Assert.AreEqual(0, loaded.ReplaceTexts.Count);
        }

        [TestMethod]
        public void Load_CorruptFile_ReturnsEmptyHistory()
        {
            string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".xml");
            try
            {
                File.WriteAllText(path, "this is not xml");
                var loaded = SearchHistory.Load(path);
                Assert.AreEqual(0, loaded.SearchTexts.Count);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void Save_CreatesMissingDirectory()
        {
            string dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            string path = Path.Combine(dir, "history.xml");
            try
            {
                var h = new SearchHistory();
                h.PushSearch("x");
                h.Save(path);
                Assert.IsTrue(File.Exists(path));
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        [TestMethod]
        public void ToDisplayLine_ReplacesNewlinesAndTruncates()
        {
            Assert.AreEqual("a↵b", SearchHistory.ToDisplayLine("a\r\nb", 60));
            Assert.AreEqual("a↵b", SearchHistory.ToDisplayLine("a\nb", 60));
            Assert.AreEqual("abcde…", SearchHistory.ToDisplayLine("abcdefghij", 5));
            Assert.AreEqual("abcde", SearchHistory.ToDisplayLine("abcde", 5));
        }
    }
}
