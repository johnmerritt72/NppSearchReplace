using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace NppSearchReplace.Core
{
    /// <summary>
    /// Most-recent-first lists of search and replacement texts. Serialised with XmlSerializer, so the
    /// public lists must stay public and settable.
    /// </summary>
    [XmlRoot("SearchHistory")]
    public sealed class SearchHistory
    {
        public const int MaxEntries = 20;

        [XmlArray("SearchTexts"), XmlArrayItem("Text")]
        public List<string> SearchTexts { get; set; } = new List<string>();

        [XmlArray("ReplaceTexts"), XmlArrayItem("Text")]
        public List<string> ReplaceTexts { get; set; } = new List<string>();

        public void PushSearch(string text) => Push(SearchTexts, text);

        public void PushReplace(string text) => Push(ReplaceTexts, text);

        private static void Push(List<string> list, string text)
        {
            if (string.IsNullOrEmpty(text))
                return;
            list.Remove(text);
            list.Insert(0, text);
            if (list.Count > MaxEntries)
                list.RemoveRange(MaxEntries, list.Count - MaxEntries);
        }

        public void Save(string path)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            var serializer = new XmlSerializer(typeof(SearchHistory));
            // Entitize keeps "\r" as &#xD; so CRLF entries survive the XML parser's newline normalisation.
            var settings = new XmlWriterSettings { Indent = true, NewLineHandling = NewLineHandling.Entitize };
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (XmlWriter writer = XmlWriter.Create(stream, settings))
                serializer.Serialize(writer, this);
        }

        public static SearchHistory Load(string path)
        {
            if (!File.Exists(path))
                return new SearchHistory();
            try
            {
                var serializer = new XmlSerializer(typeof(SearchHistory));
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    var loaded = serializer.Deserialize(stream) as SearchHistory;
                    if (loaded == null)
                        return new SearchHistory();
                    loaded.SearchTexts = loaded.SearchTexts ?? new List<string>();
                    loaded.ReplaceTexts = loaded.ReplaceTexts ?? new List<string>();
                    return loaded;
                }
            }
            catch (Exception)
            {
                return new SearchHistory();
            }
        }

        /// <summary>Single-line preview for menus: newlines become U+21B5, long text is cut with U+2026.</summary>
        public static string ToDisplayLine(string text, int maxLength)
        {
            string oneLine = (text ?? "").Replace("\r\n", "↵").Replace("\n", "↵").Replace("\r", "↵");
            if (oneLine.Length <= maxLength)
                return oneLine;
            return oneLine.Substring(0, maxLength) + "…";
        }
    }
}
