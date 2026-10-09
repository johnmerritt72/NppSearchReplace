using System;

namespace NppSearchReplace.Core
{
    /// <summary>
    /// Everything Core needs from the editor. All positions are Scintilla byte positions.
    /// The plugin implements this over ScintillaGateway; tests implement it in memory.
    /// </summary>
    public interface IEditorTarget
    {
        long Length { get; }
        int SelectionStart { get; }
        int SelectionEnd { get; }
        /// <summary>Start of the last successful search match or replacement.</summary>
        int TargetStart { get; }
        /// <summary>End of the last successful search match or replacement.</summary>
        int TargetEnd { get; }
        /// <summary>"\r\n", "\r" or "\n" depending on the document's EOL mode.</summary>
        string EolString { get; }
        IntPtr CurrentBufferId { get; }

        void SetSearchFlags(int scintillaFlags);
        /// <summary>
        /// Search for pattern in [start, end). If start &gt; end the search runs backwards over [end, start).
        /// Returns the match start, or -1. On success TargetStart/TargetEnd describe the match.
        /// </summary>
        int SearchInRange(int start, int end, string pattern);
        /// <summary>Replace the current target. Returns the byte length of the inserted text and updates TargetEnd.</summary>
        int ReplaceTarget(string replacement, bool useRegexGroups);
        /// <summary>True if the most recent search failed because the regex did not compile. Reading clears the flag.</summary>
        bool TakeRegexError();
        int PositionAfter(int position);
        int LineFromPosition(int position);
        void SetSelection(int anchor, int caret);
        void ScrollCaretIntoView();
        void BeginUndoAction();
        void EndUndoAction();

        /// <summary>Returns an indicator number owned by this plugin, or -1 if none could be allocated.</summary>
        int AllocateIndicator();
        void StyleIndicator(int indicator);
        void FillIndicator(int indicator, int start, int length);
        void ClearIndicator(int indicator, int start, int length);
    }
}
