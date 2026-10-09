using System;
using System.Collections.Generic;
using System.Text;
using NppSearchReplace.Core;

namespace NppSearchReplace.Core.Tests
{
    /// <summary>
    /// In-memory IEditorTarget. One char == one byte, so tests use ASCII text.
    /// Literal search only, except the pattern "^" which is an empty match at the search start.
    /// </summary>
    public sealed class FakeEditorTarget : IEditorTarget
    {
        public StringBuilder Text;
        public long? LengthOverride;
        public HashSet<string> InvalidPatterns = new HashSet<string>();
        public List<Tuple<int, int, int>> Fills = new List<Tuple<int, int, int>>();   // indicator, start, length
        public List<Tuple<int, int, int>> Clears = new List<Tuple<int, int, int>>();  // indicator, start, length
        public List<Tuple<int, int>> Selections = new List<Tuple<int, int>>();        // anchor, caret
        public int IndicatorToAllocate = 9;
        public int AllocateCalls;
        public int StyleCalls;
        public int ScrollCalls;
        public int UndoDepth;
        public int UndoActionsCompleted;
        public int Flags;
        private bool _regexError;

        public FakeEditorTarget(string text)
        {
            Text = new StringBuilder(text);
        }

        public long Length => LengthOverride ?? Text.Length;
        public int SelectionStart { get; set; }
        public int SelectionEnd { get; set; }
        public int TargetStart { get; private set; }
        public int TargetEnd { get; private set; }
        public string EolString { get; set; } = "\n";
        public IntPtr CurrentBufferId { get; set; } = new IntPtr(1);

        public void SetSearchFlags(int scintillaFlags) => Flags = scintillaFlags;

        public int SearchInRange(int start, int end, string pattern)
        {
            if (InvalidPatterns.Contains(pattern))
            {
                _regexError = true;
                return -1;
            }
            string s = Text.ToString();
            int idx;
            if (start <= end)
            {
                int hi = Math.Min(end, s.Length);
                if (start > hi)
                    return -1;
                if (pattern == "^")
                    idx = start;
                else
                    idx = s.IndexOf(pattern, start, hi - start, Comparison());
            }
            else
            {
                int lo = end;
                int hi = Math.Min(start, s.Length);
                if (pattern == "^")
                    idx = hi;
                else
                {
                    string slice = s.Substring(lo, hi - lo);
                    int rel = slice.LastIndexOf(pattern, Comparison());
                    idx = rel < 0 ? -1 : lo + rel;
                }
            }
            if (idx < 0)
                return -1;
            TargetStart = idx;
            TargetEnd = idx + (pattern == "^" ? 0 : pattern.Length);
            return idx;
        }

        private StringComparison Comparison()
        {
            return (Flags & SearchOptions.ScfindMatchCase) != 0 ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        }

        public int ReplaceTarget(string replacement, bool useRegexGroups)
        {
            Text.Remove(TargetStart, TargetEnd - TargetStart);
            Text.Insert(TargetStart, replacement);
            TargetEnd = TargetStart + replacement.Length;
            return replacement.Length;
        }

        public bool TakeRegexError()
        {
            bool r = _regexError;
            _regexError = false;
            return r;
        }

        public int PositionAfter(int position) => Math.Min(position + 1, Text.Length);

        public int LineFromPosition(int position)
        {
            int line = 0;
            int limit = Math.Min(position, Text.Length);
            for (int i = 0; i < limit; i++)
                if (Text[i] == '\n') line++;
            return line;
        }

        public void SetSelection(int anchor, int caret)
        {
            SelectionStart = Math.Min(anchor, caret);
            SelectionEnd = Math.Max(anchor, caret);
            Selections.Add(Tuple.Create(anchor, caret));
        }

        public void ScrollCaretIntoView() => ScrollCalls++;
        public void BeginUndoAction() => UndoDepth++;
        public void EndUndoAction() { UndoDepth--; UndoActionsCompleted++; }

        public int AllocateIndicator() { AllocateCalls++; return IndicatorToAllocate; }
        public void StyleIndicator(int indicator) => StyleCalls++;
        public void FillIndicator(int indicator, int start, int length) => Fills.Add(Tuple.Create(indicator, start, length));
        public void ClearIndicator(int indicator, int start, int length) => Clears.Add(Tuple.Create(indicator, start, length));
    }
}
