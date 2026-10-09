namespace NppSearchReplace.Core
{
    public struct TextSpan
    {
        public int Start;
        public int End;

        public TextSpan(int start, int end)
        {
            Start = start;
            End = end;
        }

        public int Length => End - Start;

        public override string ToString() => "[" + Start + "," + End + ")";
    }
}
