using System;
using System.Text;
using Kbg.NppPluginNET.PluginInfrastructure;
using NppSearchReplace.Core;
using NppSearchReplace.Utils;

namespace NppSearchReplace.Search
{
    /// <summary>
    /// IEditorTarget over whichever Scintilla view is active right now. A fresh gateway is created per call
    /// because the active view can change between calls (two-view mode).
    /// </summary>
    public sealed class ScintillaEditorTarget : IEditorTarget
    {
        private int _indicator = -1;

        private static ScintillaGateway Ed => new ScintillaGateway(PluginBase.GetCurrentScintilla());

        public long Length => Ed.GetLength();
        public int SelectionStart => Ed.GetSelectionStart();
        public int SelectionEnd => Ed.GetSelectionEnd();
        public int TargetStart => Ed.GetTargetStart();
        public int TargetEnd => Ed.GetTargetEnd();

        public string EolString
        {
            get
            {
                switch (Ed.GetEOLMode())
                {
                    case EndOfLine.CR: return "\r";
                    case EndOfLine.LF: return "\n";
                    default: return "\r\n";
                }
            }
        }

        public IntPtr CurrentBufferId =>
            Win32.SendMessage(PluginBase.nppData._nppHandle, (uint)NppMsg.NPPM_GETCURRENTBUFFERID, 0, 0);

        public void SetSearchFlags(int scintillaFlags) => Ed.SetSearchFlags((FindOption)scintillaFlags);

        public int SearchInRange(int start, int end, string pattern)
        {
            ScintillaGateway ed = Ed;
            ed.SetTargetRange(start, end);
            Encoding enc = ed.GetCodePage();
            return ed.SearchInTarget(enc.GetByteCount(pattern), pattern, enc);
        }

        public int ReplaceTarget(string replacement, bool useRegexGroups)
        {
            ScintillaGateway ed = Ed;
            Encoding enc = ed.GetCodePage();
            int len = enc.GetByteCount(replacement);
            return useRegexGroups ? ed.ReplaceTargetRE(len, replacement, enc) : ed.ReplaceTarget(len, replacement, enc);
        }

        public bool TakeRegexError()
        {
            ScintillaGateway ed = Ed;
            Status status = ed.GetStatus();
            if (status != Status.OK)
                ed.SetStatus(Status.OK);
            return status == Status.WARN_REGEX;
        }

        public int PositionAfter(int position) => Ed.PositionAfter(position);

        public int LineFromPosition(int position) => Ed.LineFromPosition(position);

        public void SetSelection(int anchor, int caret) => Ed.SetSel(anchor, caret);

        public void ScrollCaretIntoView()
        {
            ScintillaGateway ed = Ed;
            ed.EnsureVisible(ed.LineFromPosition(ed.GetCurrentPos()));
            ed.ScrollCaret();
        }

        public void BeginUndoAction() => Ed.BeginUndoAction();

        public void EndUndoAction() => Ed.EndUndoAction();

        public int AllocateIndicator()
        {
            if (_indicator < 0 && Npp.notepad.AllocateIndicators(1, out int[] ids) && ids != null && ids.Length > 0)
                _indicator = ids[0];
            return _indicator;
        }

        public void StyleIndicator(int indicator)
        {
            ScintillaGateway ed = Ed;
            ed.IndicSetStyle(indicator, IndicatorStyle.ROUNDBOX);
            ed.IndicSetFore(indicator, new Colour(0xFF, 0xA5, 0x00)); // orange, readable on light and dark themes
            ed.IndicSetAlpha(indicator, (Alpha)70);
            ed.IndicSetOutlineAlpha(indicator, (Alpha)140);
            ed.IndicSetUnder(indicator, true);
        }

        public void FillIndicator(int indicator, int start, int length)
        {
            ScintillaGateway ed = Ed;
            ed.SetIndicatorCurrent(indicator);
            ed.IndicatorFillRange(start, length);
        }

        public void ClearIndicator(int indicator, int start, int length)
        {
            ScintillaGateway ed = Ed;
            ed.SetIndicatorCurrent(indicator);
            ed.IndicatorClearRange(start, length);
        }
    }
}
