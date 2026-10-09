using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NppSearchReplace.Core.Tests
{
    [TestClass]
    public class SearchEngineFirstLastTests
    {
        private static SearchOptions NoWrap => new SearchOptions { WrapAround = false };

        [TestMethod]
        public void FindFirst_IgnoresSelectionAndSelectsFirstMatch()
        {
            var t = new FakeEditorTarget("ab xx ab xx ab") { SelectionStart = 7, SelectionEnd = 8 };
            var r = new SearchEngine(t).FindFirst("ab", NoWrap);
            Assert.IsTrue(r.Found);
            Assert.AreEqual(0, r.Start);
            Assert.AreEqual(2, r.End);
            Assert.IsFalse(r.Wrapped);
            Assert.AreEqual(0, t.SelectionStart);
            Assert.AreEqual(2, t.SelectionEnd);
            Assert.AreEqual(1, t.ScrollCalls);
        }

        [TestMethod]
        public void FindFirst_NoMatch_ReportsNotFoundAndLeavesSelection()
        {
            var t = new FakeEditorTarget("xx") { SelectionStart = 1, SelectionEnd = 1 };
            var r = new SearchEngine(t).FindFirst("ab", NoWrap);
            Assert.IsFalse(r.Found);
            Assert.AreEqual(0, t.Selections.Count);
        }

        [TestMethod]
        public void FindFirst_EmptyPattern_ReportsEmptyPattern()
        {
            var r = new SearchEngine(new FakeEditorTarget("ab")).FindFirst("", NoWrap);
            Assert.IsTrue(r.EmptyPattern);
        }

        [TestMethod]
        public void FindFirst_InvalidRegex_ReportsRegexError()
        {
            var t = new FakeEditorTarget("ab");
            t.InvalidPatterns.Add("(");
            var r = new SearchEngine(t).FindFirst("(", new SearchOptions { UseRegex = true });
            Assert.IsTrue(r.RegexError);
            Assert.IsFalse(r.Found);
        }

        [TestMethod]
        public void FindFirst_DocumentOverIntMax_ReportsTooLarge()
        {
            var t = new FakeEditorTarget("ab") { LengthOverride = (long)int.MaxValue + 1 };
            Assert.IsTrue(new SearchEngine(t).FindFirst("ab", NoWrap).TooLarge);
        }

        [TestMethod]
        public void FindLast_IgnoresSelectionAndSelectsLastMatch()
        {
            var t = new FakeEditorTarget("ab xx ab xx ab") { SelectionStart = 0, SelectionEnd = 2 };
            var r = new SearchEngine(t).FindLast("ab", NoWrap);
            Assert.IsTrue(r.Found);
            Assert.AreEqual(12, r.Start);
            Assert.AreEqual(14, r.End);
            Assert.IsFalse(r.Wrapped);
            Assert.AreEqual(12, t.SelectionStart);
            Assert.AreEqual(14, t.SelectionEnd);
        }

        [TestMethod]
        public void FindLast_ReportsLineOfMatch()
        {
            var t = new FakeEditorTarget("ab\nab\nxx");
            var r = new SearchEngine(t).FindLast("ab", NoWrap);
            Assert.AreEqual(1, r.Line);
        }

        [TestMethod]
        public void FindLast_NoMatch_ReportsNotFound()
        {
            var r = new SearchEngine(new FakeEditorTarget("xx")).FindLast("ab", NoWrap);
            Assert.IsFalse(r.Found);
        }

        [TestMethod]
        public void FindLast_EmptyPattern_ReportsEmptyPattern()
        {
            var r = new SearchEngine(new FakeEditorTarget("ab")).FindLast("", NoWrap);
            Assert.IsTrue(r.EmptyPattern);
        }
    }
}
