using System.Collections.Generic;

namespace NppSearchReplace.Core
{
    public sealed class MatchList
    {
        public List<TextSpan> Matches { get; } = new List<TextSpan>();
        public bool Capped { get; set; }
        public bool RegexError { get; set; }
        public bool TooLarge { get; set; }
    }

    /// <summary>
    /// Walks every match from the start of the document. This is the one place that knows the
    /// empty-match rule (advance one character) and the cap.
    /// </summary>
    public static class MatchEnumerator
    {
        public static MatchList Enumerate(IEditorTarget target, string pattern, SearchOptions options, int cap)
        {
            var result = new MatchList();
            if (string.IsNullOrEmpty(pattern))
                return result;
            if (target.Length > int.MaxValue)
            {
                result.TooLarge = true;
                return result;
            }
            int length = (int)target.Length;
            target.SetSearchFlags(options.ToScintillaFlags());
            int pos = 0;
            while (pos <= length)
            {
                int found = target.SearchInRange(pos, length, pattern);
                if (found < 0)
                {
                    result.RegexError = target.TakeRegexError();
                    break;
                }
                int end = target.TargetEnd;
                result.Matches.Add(new TextSpan(found, end));
                if (result.Matches.Count >= cap)
                {
                    result.Capped = true;
                    break;
                }
                if (end > found)
                {
                    pos = end;
                }
                else
                {
                    int next = target.PositionAfter(end);
                    if (next <= end)
                        break;
                    pos = next;
                }
            }
            return result;
        }
    }
}
