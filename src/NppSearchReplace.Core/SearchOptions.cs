namespace NppSearchReplace.Core
{
    /// <summary>
    /// The user's search toggles. WholeWord is ignored while UseRegex is on (Notepad++ does the same),
    /// and DotMatchesNewline only means something while UseRegex is on.
    /// </summary>
    public sealed class SearchOptions
    {
        public const int ScfindWholeWord = 0x2;
        public const int ScfindMatchCase = 0x4;
        public const int ScfindRegexp = 0x00200000;
        public const int ScfindPosix = 0x00400000;
        /// <summary>Notepad++-specific extension to Scintilla's search flags.</summary>
        public const int ScfindRegexpDotMatchesNewline = 0x10000000;

        public bool UseRegex { get; set; }
        public bool DotMatchesNewline { get; set; }
        public bool MatchCase { get; set; }
        public bool WholeWord { get; set; }
        public bool WrapAround { get; set; } = true;

        public int ToScintillaFlags()
        {
            int flags = 0;
            if (MatchCase)
                flags |= ScfindMatchCase;
            if (UseRegex)
            {
                flags |= ScfindRegexp | ScfindPosix;
                if (DotMatchesNewline)
                    flags |= ScfindRegexpDotMatchesNewline;
            }
            else if (WholeWord)
            {
                flags |= ScfindWholeWord;
            }
            return flags;
        }

        public SearchOptions Clone()
        {
            return new SearchOptions
            {
                UseRegex = UseRegex,
                DotMatchesNewline = DotMatchesNewline,
                MatchCase = MatchCase,
                WholeWord = WholeWord,
                WrapAround = WrapAround,
            };
        }
    }
}
