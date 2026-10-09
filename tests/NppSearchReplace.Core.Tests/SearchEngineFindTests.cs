using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NppSearchReplace.Core.Tests
{
    [TestClass]
    public class SearchEngineFindTests
    {
        private static SearchOptions Wrap => new SearchOptions { WrapAround = true };
        private static SearchOptions NoWrap => new SearchOptions { WrapAround = false };

        [TestMethod]
        public void FindNext_SearchesFromSelectionEndAndSelectsMatch()
        {
            var t = new FakeEditorTarget("xx ab xx ab") { SelectionStart = 0, SelectionEnd = 0 };
            var r = new SearchEngine(t).FindNext("ab", Wrap);
            Assert.IsTrue(r.Found);
            Assert.AreEqual(3, r.Start);
            Assert.AreEqual(5, r.End);
            Assert.IsFalse(r.Wrapped);
            Assert.AreEqual(3, t.SelectionStart);
            Assert.AreEqual(5, t.SelectionEnd);
            Assert.AreEqual(1, t.ScrollCalls);
        }

        [TestMethod]
        public void FindNext_SecondCall_FindsNextOccurrence()
        {
            var t = new FakeEditorTarget("ab ab");
            var e = new SearchEngine(t);
            e.FindNext("ab", Wrap);
            var r = e.FindNext("ab", Wrap);
            Assert.AreEqual(3, r.Start);
        }

        [TestMethod]
        public void FindNext_ReportsZeroBasedLine()
        {
            var t = new FakeEditorTarget("a\nb\nab");
            var r = new SearchEngine(t).FindNext("ab", Wrap);
            Assert.AreEqual(2, r.Line);
        }

        [TestMethod]
        public void FindNext_AtEndWithWrap_WrapsToStartAndFlagsIt()
        {
            var t = new FakeEditorTarget("ab xx") { SelectionStart = 3, SelectionEnd = 3 };
            var r = new SearchEngine(t).FindNext("ab", Wrap);
            Assert.IsTrue(r.Found);
            Assert.IsTrue(r.Wrapped);
            Assert.AreEqual(0, r.Start);
        }

        [TestMethod]
        public void FindNext_AtEndWithoutWrap_ReportsNotFoundAndLeavesSelection()
        {
            var t = new FakeEditorTarget("ab xx") { SelectionStart = 3, SelectionEnd = 3 };
            var r = new SearchEngine(t).FindNext("ab", NoWrap);
            Assert.IsFalse(r.Found);
            Assert.IsFalse(r.Wrapped);
            Assert.AreEqual(0, t.Selections.Count);
        }

        [TestMethod]
        public void FindNext_EmptyPattern_ReportsEmptyPattern()
        {
            var t = new FakeEditorTarget("ab");
            var r = new SearchEngine(t).FindNext("", Wrap);
            Assert.IsTrue(r.EmptyPattern);
            Assert.IsFalse(r.Found);
        }

        [TestMethod]
        public void FindNext_InvalidRegex_ReportsRegexErrorWithoutWrapping()
        {
            var t = new FakeEditorTarget("ab") { SelectionStart = 1, SelectionEnd = 1 };
            t.InvalidPatterns.Add("(");
            var r = new SearchEngine(t).FindNext("(", new SearchOptions { UseRegex = true, WrapAround = true });
            Assert.IsTrue(r.RegexError);
            Assert.IsFalse(r.Found);
            Assert.IsFalse(r.Wrapped);
        }

        [TestMethod]
        public void FindNext_EmptyMatchAtCurrentSelection_AdvancesInsteadOfSticking()
        {
            var t = new FakeEditorTarget("abc") { SelectionStart = 1, SelectionEnd = 1 };
            var r = new SearchEngine(t).FindNext("^", new SearchOptions { UseRegex = true });
            Assert.IsTrue(r.Found);
            Assert.AreEqual(2, r.Start);
            Assert.AreEqual(2, r.End);
        }

        [TestMethod]
        public void FindNext_DocumentOverIntMax_ReportsTooLarge()
        {
            var t = new FakeEditorTarget("ab") { LengthOverride = (long)int.MaxValue + 1 };
            var r = new SearchEngine(t).FindNext("ab", Wrap);
            Assert.IsTrue(r.TooLarge);
            Assert.IsFalse(r.Found);
        }

        [TestMethod]
        public void FindNext_NormalisesPatternNewlinesToDocumentEol()
        {
            var t = new FakeEditorTarget("one\ntwo") { EolString = "\n" };
            var r = new SearchEngine(t).FindNext("one\r\ntwo", Wrap);
            Assert.IsTrue(r.Found);
            Assert.AreEqual(0, r.Start);
            Assert.AreEqual(7, r.End);
        }

        [TestMethod]
        public void NormalizeNewlines_ConvertsAllThreeFormsToEol()
        {
            var t = new FakeEditorTarget("") { EolString = "\r\n" };
            var e = new SearchEngine(t);
            Assert.AreEqual("a\r\nb\r\nc\r\nd", e.NormalizeNewlines("a\r\nb\nc\rd"));
        }

        [TestMethod]
        public void FindPrevious_SearchesBackwardFromSelectionStart()
        {
            var t = new FakeEditorTarget("ab xx ab") { SelectionStart = 6, SelectionEnd = 8 };
            var r = new SearchEngine(t).FindPrevious("ab", Wrap);
            Assert.IsTrue(r.Found);
            Assert.AreEqual(0, r.Start);
            Assert.IsFalse(r.Wrapped);
        }

        [TestMethod]
        public void FindPrevious_AtStartWithWrap_WrapsToEnd()
        {
            var t = new FakeEditorTarget("xx ab") { SelectionStart = 0, SelectionEnd = 0 };
            var r = new SearchEngine(t).FindPrevious("ab", Wrap);
            Assert.IsTrue(r.Found);
            Assert.IsTrue(r.Wrapped);
            Assert.AreEqual(3, r.Start);
        }

        [TestMethod]
        public void FindPrevious_AtStartWithoutWrap_ReportsNotFound()
        {
            var t = new FakeEditorTarget("xx ab") { SelectionStart = 0, SelectionEnd = 0 };
            var r = new SearchEngine(t).FindPrevious("ab", NoWrap);
            Assert.IsFalse(r.Found);
        }
    }
}
