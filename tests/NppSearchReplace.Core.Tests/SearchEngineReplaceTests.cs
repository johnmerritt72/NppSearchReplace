using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NppSearchReplace.Core.Tests
{
    [TestClass]
    public class SearchEngineReplaceTests
    {
        private static SearchOptions Wrap => new SearchOptions { WrapAround = true };

        [TestMethod]
        public void Replace_SelectionIsAMatch_ReplacesThenFindsNext()
        {
            var t = new FakeEditorTarget("ab ab") { SelectionStart = 0, SelectionEnd = 2 };
            var r = new SearchEngine(t).Replace("ab", "XYZ", Wrap);
            Assert.IsTrue(r.Replaced);
            Assert.AreEqual("XYZ ab", t.Text.ToString());
            Assert.IsTrue(r.Next.Found);
            Assert.AreEqual(4, r.Next.Start);
        }

        [TestMethod]
        public void Replace_SelectionNotAMatch_DoesNotReplaceButFindsNext()
        {
            var t = new FakeEditorTarget("zz ab") { SelectionStart = 0, SelectionEnd = 2 };
            var r = new SearchEngine(t).Replace("ab", "X", Wrap);
            Assert.IsFalse(r.Replaced);
            Assert.AreEqual("zz ab", t.Text.ToString());
            Assert.IsTrue(r.Next.Found);
            Assert.AreEqual(3, r.Next.Start);
        }

        [TestMethod]
        public void Replace_SelectionLongerThanMatch_DoesNotReplace()
        {
            var t = new FakeEditorTarget("abc ab") { SelectionStart = 0, SelectionEnd = 3 };
            var r = new SearchEngine(t).Replace("ab", "X", Wrap);
            Assert.IsFalse(r.Replaced);
            Assert.AreEqual("abc ab", t.Text.ToString());
        }

        [TestMethod]
        public void Replace_EmptySelection_OnlyFindsNext()
        {
            var t = new FakeEditorTarget("ab") { SelectionStart = 0, SelectionEnd = 0 };
            var r = new SearchEngine(t).Replace("ab", "X", Wrap);
            Assert.IsFalse(r.Replaced);
            Assert.IsTrue(r.Next.Found);
        }

        [TestMethod]
        public void Replace_LastMatchWithoutWrap_ReplacesAndReportsNoMore()
        {
            var t = new FakeEditorTarget("ab") { SelectionStart = 0, SelectionEnd = 2 };
            var r = new SearchEngine(t).Replace("ab", "X", new SearchOptions { WrapAround = false });
            Assert.IsTrue(r.Replaced);
            Assert.AreEqual("X", t.Text.ToString());
            Assert.IsFalse(r.Next.Found);
        }

        [TestMethod]
        public void Replace_PassesRegexFlagToReplaceTarget()
        {
            var t = new FakeEditorTarget("ab") { SelectionStart = 0, SelectionEnd = 2 };
            var r = new SearchEngine(t).Replace("ab", "X", new SearchOptions { UseRegex = true });
            Assert.IsTrue(r.Replaced);
            Assert.AreEqual(SearchOptions.ScfindRegexp | SearchOptions.ScfindPosix, t.Flags);
        }

        [TestMethod]
        public void Replace_EmptyPattern_ReportsEmptyPattern()
        {
            var t = new FakeEditorTarget("ab") { SelectionStart = 0, SelectionEnd = 2 };
            var r = new SearchEngine(t).Replace("", "X", Wrap);
            Assert.IsFalse(r.Replaced);
            Assert.IsTrue(r.Next.EmptyPattern);
        }

        [TestMethod]
        public void ReplaceAll_ReplacesEveryMatchInsideOneUndoAction()
        {
            var t = new FakeEditorTarget("ab-ab-ab");
            var r = new SearchEngine(t).ReplaceAll("ab", "LONGER", Wrap);
            Assert.AreEqual(3, r.Count);
            Assert.AreEqual("LONGER-LONGER-LONGER", t.Text.ToString());
            Assert.AreEqual(1, t.UndoActionsCompleted);
            Assert.AreEqual(0, t.UndoDepth);
        }

        [TestMethod]
        public void ReplaceAll_ShorterReplacement_AdjustsOffsets()
        {
            var t = new FakeEditorTarget("abcabcabc");
            var r = new SearchEngine(t).ReplaceAll("abc", "x", Wrap);
            Assert.AreEqual(3, r.Count);
            Assert.AreEqual("xxx", t.Text.ToString());
        }

        [TestMethod]
        public void ReplaceAll_ReplacementContainsPattern_DoesNotLoopForever()
        {
            var t = new FakeEditorTarget("a a");
            var r = new SearchEngine(t).ReplaceAll("a", "aa", Wrap);
            Assert.AreEqual(2, r.Count);
            Assert.AreEqual("aa aa", t.Text.ToString());
        }

        [TestMethod]
        public void ReplaceAll_EmptyMatches_InsertAtEachPositionAndTerminate()
        {
            var t = new FakeEditorTarget("ab");
            var r = new SearchEngine(t).ReplaceAll("^", "-", new SearchOptions { UseRegex = true });
            Assert.AreEqual(3, r.Count);
            Assert.AreEqual("-a-b-", t.Text.ToString());
        }

        [TestMethod]
        public void ReplaceAll_NoMatches_ReturnsZeroAndStillClosesUndo()
        {
            var t = new FakeEditorTarget("zzz");
            var r = new SearchEngine(t).ReplaceAll("ab", "X", Wrap);
            Assert.AreEqual(0, r.Count);
            Assert.AreEqual(1, t.UndoActionsCompleted);
        }

        [TestMethod]
        public void ReplaceAll_InvalidRegex_ReportsErrorAndChangesNothing()
        {
            var t = new FakeEditorTarget("abc");
            t.InvalidPatterns.Add("(");
            var r = new SearchEngine(t).ReplaceAll("(", "X", new SearchOptions { UseRegex = true });
            Assert.IsTrue(r.RegexError);
            Assert.AreEqual(0, r.Count);
            Assert.AreEqual("abc", t.Text.ToString());
            Assert.AreEqual(0, t.UndoDepth);
        }

        [TestMethod]
        public void ReplaceAll_NormalisesReplacementNewlines()
        {
            var t = new FakeEditorTarget("a,b") { EolString = "\n" };
            new SearchEngine(t).ReplaceAll(",", "\r\n", Wrap);
            Assert.AreEqual("a\nb", t.Text.ToString());
        }

        [TestMethod]
        public void ReplaceAll_DocumentOverIntMax_ReportsTooLarge()
        {
            var t = new FakeEditorTarget("ab") { LengthOverride = (long)int.MaxValue + 1 };
            var r = new SearchEngine(t).ReplaceAll("ab", "X", Wrap);
            Assert.IsTrue(r.TooLarge);
            Assert.AreEqual(0, t.UndoActionsCompleted);
        }

        [TestMethod]
        public void Count_ReturnsNumberOfMatchesWithoutTouchingSelection()
        {
            var t = new FakeEditorTarget("ab ab ab") { SelectionStart = 1, SelectionEnd = 1 };
            var r = new SearchEngine(t).Count("ab", Wrap, 1000);
            Assert.AreEqual(3, r.Count);
            Assert.IsFalse(r.Capped);
            Assert.AreEqual(0, t.Selections.Count);
            Assert.AreEqual("ab ab ab", t.Text.ToString());
        }

        [TestMethod]
        public void Count_HonoursCap()
        {
            var t = new FakeEditorTarget(new string('a', 30));
            var r = new SearchEngine(t).Count("a", Wrap, 5);
            Assert.AreEqual(5, r.Count);
            Assert.IsTrue(r.Capped);
        }

        [TestMethod]
        public void Count_EmptyPattern_ReportsEmptyPattern()
        {
            var t = new FakeEditorTarget("ab");
            var r = new SearchEngine(t).Count("", Wrap, 1000);
            Assert.IsTrue(r.EmptyPattern);
            Assert.AreEqual(0, r.Count);
        }
    }
}
