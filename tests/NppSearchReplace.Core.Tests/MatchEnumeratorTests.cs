using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NppSearchReplace.Core.Tests
{
    [TestClass]
    public class MatchEnumeratorTests
    {
        private static readonly SearchOptions Literal = new SearchOptions();

        [TestMethod]
        public void Enumerate_FindsEveryNonOverlappingMatchInOrder()
        {
            var t = new FakeEditorTarget("ab ab ab");
            var r = MatchEnumerator.Enumerate(t, "ab", Literal, 100);
            CollectionAssert.AreEqual(new[] { "[0,2)", "[3,5)", "[6,8)" }, r.Matches.Select(m => m.ToString()).ToArray());
            Assert.IsFalse(r.Capped);
            Assert.IsFalse(r.RegexError);
        }

        [TestMethod]
        public void Enumerate_OverlappingCandidates_ResumeAtMatchEnd()
        {
            var t = new FakeEditorTarget("aaaa");
            var r = MatchEnumerator.Enumerate(t, "aa", Literal, 100);
            Assert.AreEqual(2, r.Matches.Count);
        }

        [TestMethod]
        public void Enumerate_NoMatch_ReturnsEmpty()
        {
            var t = new FakeEditorTarget("hello");
            var r = MatchEnumerator.Enumerate(t, "zzz", Literal, 100);
            Assert.AreEqual(0, r.Matches.Count);
        }

        [TestMethod]
        public void Enumerate_EmptyPattern_ReturnsEmptyWithoutSearching()
        {
            var t = new FakeEditorTarget("hello");
            var r = MatchEnumerator.Enumerate(t, "", Literal, 100);
            Assert.AreEqual(0, r.Matches.Count);
            Assert.AreEqual(0, t.Flags);
        }

        [TestMethod]
        public void Enumerate_EmptyMatches_AdvanceOnePositionAndTerminate()
        {
            var t = new FakeEditorTarget("abc");
            var r = MatchEnumerator.Enumerate(t, "^", Literal, 100);
            // empty match at 0,1,2,3 then PositionAfter(3)==3 stops the loop
            Assert.AreEqual(4, r.Matches.Count);
            Assert.IsTrue(r.Matches.All(m => m.Length == 0));
        }

        [TestMethod]
        public void Enumerate_StopsAtCapAndFlagsIt()
        {
            var t = new FakeEditorTarget(new string('a', 50));
            var r = MatchEnumerator.Enumerate(t, "a", Literal, 10);
            Assert.AreEqual(10, r.Matches.Count);
            Assert.IsTrue(r.Capped);
        }

        [TestMethod]
        public void Enumerate_InvalidRegex_ReportsErrorAndNoMatches()
        {
            var t = new FakeEditorTarget("abc");
            t.InvalidPatterns.Add("(");
            var r = MatchEnumerator.Enumerate(t, "(", new SearchOptions { UseRegex = true }, 100);
            Assert.AreEqual(0, r.Matches.Count);
            Assert.IsTrue(r.RegexError);
            Assert.IsFalse(t.TakeRegexError(), "error flag must be consumed");
        }

        [TestMethod]
        public void Enumerate_SetsSearchFlagsFromOptions()
        {
            var t = new FakeEditorTarget("Ab ab");
            var r = MatchEnumerator.Enumerate(t, "ab", new SearchOptions { MatchCase = true }, 100);
            Assert.AreEqual(SearchOptions.ScfindMatchCase, t.Flags);
            Assert.AreEqual(1, r.Matches.Count);
        }

        [TestMethod]
        public void Enumerate_DocumentOverIntMax_ReportsTooLarge()
        {
            var t = new FakeEditorTarget("abc") { LengthOverride = (long)int.MaxValue + 1 };
            var r = MatchEnumerator.Enumerate(t, "a", Literal, 100);
            Assert.IsTrue(r.TooLarge);
            Assert.AreEqual(0, r.Matches.Count);
        }
    }
}
