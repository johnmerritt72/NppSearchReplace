namespace NppSearchReplace.Core
{
    public sealed class SearchResult
    {
        public bool Found { get; set; }
        public int Start { get; set; }
        public int End { get; set; }
        /// <summary>Zero-based line of Start.</summary>
        public int Line { get; set; }
        public bool Wrapped { get; set; }
        public bool RegexError { get; set; }
        public bool EmptyPattern { get; set; }
        public bool TooLarge { get; set; }
    }

    /// <summary>
    /// Find/replace over an IEditorTarget. Stateless apart from the target; the caret and selection
    /// in the editor are the only cursor.
    /// </summary>
    public sealed class SearchEngine
    {
        private readonly IEditorTarget _t;

        public SearchEngine(IEditorTarget target)
        {
            _t = target;
        }

        public SearchResult FindNext(string pattern, SearchOptions options) => Find(pattern, options, forward: true);

        public SearchResult FindPrevious(string pattern, SearchOptions options) => Find(pattern, options, forward: false);

        /// <summary>Newlines typed in the panel become the document's own EOL so literal multiline searches match.</summary>
        public string NormalizeNewlines(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text ?? "";
            string eol = _t.EolString;
            return text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", eol);
        }

        private SearchResult Find(string pattern, SearchOptions options, bool forward)
        {
            var r = new SearchResult();
            if (string.IsNullOrEmpty(pattern))
            {
                r.EmptyPattern = true;
                return r;
            }
            if (_t.Length > int.MaxValue)
            {
                r.TooLarge = true;
                return r;
            }
            int length = (int)_t.Length;
            string p = NormalizeNewlines(pattern);
            _t.SetSearchFlags(options.ToScintillaFlags());
            int selStart = _t.SelectionStart;
            int selEnd = _t.SelectionEnd;

            int found = forward ? _t.SearchInRange(selEnd, length, p) : _t.SearchInRange(selStart, 0, p);
            if (found < 0 && _t.TakeRegexError())
            {
                r.RegexError = true;
                return r;
            }
            if (forward && found >= 0 && found == selStart && _t.TargetEnd == selEnd && found == _t.TargetEnd)
            {
                // the match is the empty selection we already have; step past it
                int next = _t.PositionAfter(selEnd);
                found = next > selEnd ? _t.SearchInRange(next, length, p) : -1;
            }
            if (found < 0 && options.WrapAround)
            {
                found = forward ? _t.SearchInRange(0, selEnd, p) : _t.SearchInRange(length, selStart, p);
                if (found >= 0)
                    r.Wrapped = true;
            }
            if (found < 0)
                return r;

            r.Found = true;
            r.Start = found;
            r.End = _t.TargetEnd;
            r.Line = _t.LineFromPosition(found);
            _t.SetSelection(r.Start, r.End);
            _t.ScrollCaretIntoView();
            return r;
        }
    }
}
