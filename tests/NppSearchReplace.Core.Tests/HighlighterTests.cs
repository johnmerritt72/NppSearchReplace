using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NppSearchReplace.Core.Tests
{
    [TestClass]
    public class HighlighterTests
    {
        private static readonly SearchOptions Literal = new SearchOptions();

        [TestMethod]
        public void Apply_AllocatesIndicatorOnceAndStylesIt()
        {
            var t = new FakeEditorTarget("ab ab") { IndicatorToAllocate = 12 };
            var h = new Highlighter(t, 100);
            h.Apply("ab", Literal);
            h.Apply("ab", Literal);
            Assert.AreEqual(1, t.AllocateCalls);
            Assert.IsTrue(t.StyleCalls >= 1);
            Assert.IsTrue(t.Fills.All(f => f.Item1 == 12));
        }

        [TestMethod]
        public void Apply_ClearsWholeDocumentThenFillsEachMatch()
        {
            var t = new FakeEditorTarget("ab-ab");
            var r = new Highlighter(t, 100).Apply("ab", Literal);
            Assert.AreEqual(2, r.Count);
            Assert.AreEqual(1, t.Clears.Count);
            Assert.AreEqual(Tuple.Create(9, 0, 5), t.Clears[0]);
            CollectionAssert.AreEqual(new[] { Tuple.Create(9, 0, 2), Tuple.Create(9, 3, 2) }, t.Fills);
        }

        [TestMethod]
        public void Apply_EmptyMatches_AreCountedButNotFilled()
        {
            var t = new FakeEditorTarget("ab");
            var r = new Highlighter(t, 100).Apply("^", new SearchOptions { UseRegex = true });
            Assert.AreEqual(3, r.Count);
            Assert.AreEqual(0, t.Fills.Count);
        }

        [TestMethod]
        public void Apply_EmptyPattern_ClearsAndReportsZero()
        {
            var t = new FakeEditorTarget("ab");
            var h = new Highlighter(t, 100);
            h.Apply("ab", Literal);
            t.Fills.Clear(); t.Clears.Clear();
            var r = h.Apply("", Literal);
            Assert.AreEqual(0, r.Count);
            Assert.AreEqual(1, t.Clears.Count);
            Assert.AreEqual(0, t.Fills.Count);
        }

        [TestMethod]
        public void Apply_Capped_FillsUpToCapAndFlags()
        {
            var t = new FakeEditorTarget(new string('a', 40));
            var r = new Highlighter(t, 7).Apply("a", Literal);
            Assert.IsTrue(r.Capped);
            Assert.AreEqual(7, r.Count);
            Assert.AreEqual(7, t.Fills.Count);
        }

        [TestMethod]
        public void Apply_InvalidRegex_ClearsPreviousHighlightAndReportsError()
        {
            var t = new FakeEditorTarget("ab");
            var h = new Highlighter(t, 100);
            h.Apply("ab", Literal);
            t.Fills.Clear(); t.Clears.Clear();
            t.InvalidPatterns.Add("(");
            var r = h.Apply("(", new SearchOptions { UseRegex = true });
            Assert.IsTrue(r.RegexError);
            Assert.AreEqual(1, t.Clears.Count);
            Assert.AreEqual(0, t.Fills.Count);
        }

        [TestMethod]
        public void Apply_NoIndicatorAvailable_ReportsAndDoesNothing()
        {
            var t = new FakeEditorTarget("ab") { IndicatorToAllocate = -1 };
            var r = new Highlighter(t, 100).Apply("ab", Literal);
            Assert.IsTrue(r.IndicatorUnavailable);
            Assert.AreEqual(0, t.Fills.Count);
            Assert.AreEqual(0, t.Clears.Count);
        }

        [TestMethod]
        public void Apply_DocumentOverIntMax_ReportsTooLarge()
        {
            var t = new FakeEditorTarget("ab") { LengthOverride = (long)int.MaxValue + 1 };
            var r = new Highlighter(t, 100).Apply("ab", Literal);
            Assert.IsTrue(r.TooLarge);
            Assert.AreEqual(0, t.Fills.Count);
        }

        [TestMethod]
        public void TouchedBuffers_TrackApplyClearAndForget()
        {
            var t = new FakeEditorTarget("ab") { CurrentBufferId = new IntPtr(42) };
            var h = new Highlighter(t, 100);
            Assert.IsFalse(h.IsTouched(new IntPtr(42)));
            h.Apply("ab", Literal);
            Assert.IsTrue(h.IsTouched(new IntPtr(42)));
            h.ClearCurrent();
            Assert.IsFalse(h.IsTouched(new IntPtr(42)));
            h.Apply("ab", Literal);
            h.Forget(new IntPtr(42));
            Assert.IsFalse(h.IsTouched(new IntPtr(42)));
        }

        [TestMethod]
        public void Apply_WithNoMatches_DoesNotMarkBufferTouched()
        {
            var t = new FakeEditorTarget("zz") { CurrentBufferId = new IntPtr(7) };
            var h = new Highlighter(t, 100);
            h.Apply("ab", Literal);
            Assert.IsFalse(h.IsTouched(new IntPtr(7)));
        }

        [TestMethod]
        public void ClearCurrent_BeforeAnyApply_IsANoOp()
        {
            var t = new FakeEditorTarget("ab");
            new Highlighter(t, 100).ClearCurrent();
            Assert.AreEqual(0, t.Clears.Count);
            Assert.AreEqual(0, t.AllocateCalls);
        }
    }
}
