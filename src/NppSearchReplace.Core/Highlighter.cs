using System;
using System.Collections.Generic;

namespace NppSearchReplace.Core
{
    public sealed class HighlightResult
    {
        public int Count { get; set; }
        public bool Capped { get; set; }
        public bool RegexError { get; set; }
        public bool TooLarge { get; set; }
        public bool IndicatorUnavailable { get; set; }
    }

    /// <summary>
    /// Paints every match in the active document with one plugin-owned indicator and remembers
    /// which buffers it has painted so they can be cleaned up later.
    /// </summary>
    public sealed class Highlighter
    {
        private readonly IEditorTarget _t;
        private readonly int _cap;
        private int _indicator = -1;
        private readonly HashSet<IntPtr> _touched = new HashSet<IntPtr>();

        public Highlighter(IEditorTarget target, int cap)
        {
            _t = target;
            _cap = cap;
        }

        public HighlightResult Apply(string pattern, SearchOptions options)
        {
            var r = new HighlightResult();
            if (_indicator < 0)
                _indicator = _t.AllocateIndicator();
            if (_indicator < 0)
            {
                r.IndicatorUnavailable = true;
                return r;
            }
            _t.StyleIndicator(_indicator);
            ClearCurrent();

            var matches = MatchEnumerator.Enumerate(_t, pattern, options, _cap);
            r.RegexError = matches.RegexError;
            r.Capped = matches.Capped;
            r.TooLarge = matches.TooLarge;
            r.Count = matches.Matches.Count;
            if (r.Count == 0)
                return r;

            foreach (TextSpan m in matches.Matches)
            {
                if (m.Length > 0)
                    _t.FillIndicator(_indicator, m.Start, m.Length);
            }
            _touched.Add(_t.CurrentBufferId);
            return r;
        }

        /// <summary>Remove the indicator from the whole active document.</summary>
        public void ClearCurrent()
        {
            if (_indicator < 0)
                return;
            long length = _t.Length;
            if (length > int.MaxValue)
                return;
            _t.ClearIndicator(_indicator, 0, (int)length);
            _touched.Remove(_t.CurrentBufferId);
        }

        public bool IsTouched(IntPtr bufferId) => _touched.Contains(bufferId);

        public void Forget(IntPtr bufferId) => _touched.Remove(bufferId);
    }
}
