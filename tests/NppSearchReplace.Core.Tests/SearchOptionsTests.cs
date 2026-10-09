using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NppSearchReplace.Core.Tests
{
    [TestClass]
    public class SearchOptionsTests
    {
        [TestMethod]
        public void Defaults_AreLiteralCaseInsensitiveWrapping()
        {
            var o = new SearchOptions();
            Assert.IsFalse(o.UseRegex);
            Assert.IsFalse(o.DotMatchesNewline);
            Assert.IsFalse(o.MatchCase);
            Assert.IsFalse(o.WholeWord);
            Assert.IsTrue(o.WrapAround);
            Assert.AreEqual(0, o.ToScintillaFlags());
        }

        [TestMethod]
        public void MatchCase_SetsScfindMatchCase()
        {
            var o = new SearchOptions { MatchCase = true };
            Assert.AreEqual(SearchOptions.ScfindMatchCase, o.ToScintillaFlags());
        }

        [TestMethod]
        public void WholeWord_WithoutRegex_SetsScfindWholeWord()
        {
            var o = new SearchOptions { WholeWord = true };
            Assert.AreEqual(SearchOptions.ScfindWholeWord, o.ToScintillaFlags());
        }

        [TestMethod]
        public void WholeWord_WithRegex_IsIgnored()
        {
            var o = new SearchOptions { WholeWord = true, UseRegex = true };
            Assert.AreEqual(SearchOptions.ScfindRegexp | SearchOptions.ScfindPosix, o.ToScintillaFlags());
        }

        [TestMethod]
        public void Regex_SetsRegexpAndPosix()
        {
            var o = new SearchOptions { UseRegex = true };
            Assert.AreEqual(SearchOptions.ScfindRegexp | SearchOptions.ScfindPosix, o.ToScintillaFlags());
        }

        [TestMethod]
        public void DotMatchesNewline_WithRegex_SetsNotepadPlusPlusFlag()
        {
            var o = new SearchOptions { UseRegex = true, DotMatchesNewline = true };
            int expected = SearchOptions.ScfindRegexp | SearchOptions.ScfindPosix | SearchOptions.ScfindRegexpDotMatchesNewline;
            Assert.AreEqual(expected, o.ToScintillaFlags());
        }

        [TestMethod]
        public void DotMatchesNewline_WithoutRegex_IsIgnored()
        {
            var o = new SearchOptions { DotMatchesNewline = true };
            Assert.AreEqual(0, o.ToScintillaFlags());
        }

        [TestMethod]
        public void AllOn_CombinesEverythingExceptWholeWord()
        {
            var o = new SearchOptions { UseRegex = true, DotMatchesNewline = true, MatchCase = true, WholeWord = true, WrapAround = true };
            int expected = SearchOptions.ScfindMatchCase | SearchOptions.ScfindRegexp | SearchOptions.ScfindPosix | SearchOptions.ScfindRegexpDotMatchesNewline;
            Assert.AreEqual(expected, o.ToScintillaFlags());
        }

        [TestMethod]
        public void Clone_CopiesEveryToggle()
        {
            var o = new SearchOptions { UseRegex = true, DotMatchesNewline = true, MatchCase = true, WholeWord = true, WrapAround = false };
            var c = o.Clone();
            Assert.AreNotSame(o, c);
            Assert.IsTrue(c.UseRegex); Assert.IsTrue(c.DotMatchesNewline); Assert.IsTrue(c.MatchCase); Assert.IsTrue(c.WholeWord); Assert.IsFalse(c.WrapAround);
        }
    }
}
