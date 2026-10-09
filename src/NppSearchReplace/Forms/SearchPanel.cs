using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Kbg.NppPluginNET;
using NppSearchReplace.Core;
using NppSearchReplace.Search;
using NppSearchReplace.Utils;

namespace NppSearchReplace.Forms
{
    /// <summary>
    /// The docked panel. Layout is built in code (no designer/resx). All editor work goes through
    /// SearchEngine; this class only translates clicks and keys into engine calls and status text.
    /// </summary>
    public class SearchPanel : FormBase
    {
        private const int StatusErrorColor = unchecked((int)0xFFC03030);

        private readonly IEditorTarget _editor = new ScintillaEditorTarget();
        private readonly SearchEngine _engine;
        private readonly Highlighter _highlighter;
        private readonly Timer _highlightTimer = new Timer { Interval = 300 };
        private bool _suppressModifiedNotifications;
        private const int ScModInsertText = 0x1;
        private const int ScModDeleteText = 0x2;

        private CheckBox _regexToggle, _dotToggle, _caseToggle, _wordsToggle, _loopToggle, _highlightToggle;
        private Button _findNextButton, _findPrevButton, _replaceButton, _replaceAllButton, _countButton;
        private SplitContainer _split;
        private MultilineTextBox _searchBox, _replaceBox;
        private Label _statusLabel;
        private Color _statusDefaultColor;

        public SearchPanel() : base(isModal: false, isDocking: true)
        {
            _engine = new SearchEngine(_editor);
            _highlighter = new Highlighter(_editor, Math.Max(1, Main.settings.highlight_cap));
            _highlightTimer.Tick += (s, e) => { _highlightTimer.Stop(); RefreshHighlight(); };
            Text = "Search & Replace";
            BuildLayout();
            LoadOptionsFromSettings();
        }

        public SearchOptions CurrentOptions => new SearchOptions
        {
            UseRegex = _regexToggle.Checked,
            DotMatchesNewline = _dotToggle.Checked,
            MatchCase = _caseToggle.Checked,
            WholeWord = _wordsToggle.Checked,
            WrapAround = _loopToggle.Checked,
        };

        public string SearchText => _searchBox.Text;
        public string ReplaceText => _replaceBox.Text;
        public bool HighlightEnabled => _highlightToggle.Checked;

        /// <summary>Put the caret in the search box, optionally replacing its text first.</summary>
        public void FocusSearchBox(string prefill)
        {
            if (!string.IsNullOrEmpty(prefill))
                _searchBox.Text = prefill;
            _searchBox.SelectAll();
            _searchBox.Focus();
        }

        #region layout

        private void BuildLayout()
        {
            SuspendLayout();
            MinimumSize = new Size(400, 120);
            ClientSize = new Size(900, 180);
            Font = SystemFonts.MessageBoxFont;

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(4, 2, 4, 2) };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var optionsRow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false, Margin = new Padding(0) };
            _regexToggle = MakeToggle("Regex", "Regular expression search (Notepad++ / Boost syntax)");
            _dotToggle = MakeToggle("Dot", "'.' also matches newline (regex only)");
            _caseToggle = MakeToggle("Case", "Match case");
            _wordsToggle = MakeToggle("Words", "Whole word only (ignored when Regex is on)");
            _loopToggle = MakeToggle("Loop", "Wrap around at the end or start of the document");
            _highlightToggle = MakeToggle("Highlight", "Highlight every match in the document while typing");
            optionsRow.Controls.AddRange(new Control[] { _regexToggle, _dotToggle, _caseToggle, _wordsToggle, _loopToggle, _highlightToggle });

            var actionsRow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false, Margin = new Padding(0) };
            _findNextButton = MakeButton("Find Next", "Ctrl+Enter", (s, e) => DoFindNext());
            _findPrevButton = MakeButton("Find Prev", "Ctrl+Shift+Enter", (s, e) => DoFindPrevious());
            _replaceButton = MakeButton("Replace", "Ctrl+R", (s, e) => DoReplace());
            _replaceAllButton = MakeButton("Replace All", "Ctrl+Shift+R", (s, e) => DoReplaceAll());
            _countButton = MakeButton("Count", "Ctrl+Shift+C", (s, e) => DoCount());
            actionsRow.Controls.AddRange(new Control[] { _findNextButton, _findPrevButton, _replaceButton, _replaceAllButton, _countButton });

            _split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterWidth = 6, Margin = new Padding(0, 2, 0, 2) };
            _searchBox = MakeBox();
            _replaceBox = MakeBox();
            _split.Panel1.Controls.Add(_searchBox);
            _split.Panel2.Controls.Add(_replaceBox);

            _statusLabel = new Label { Dock = DockStyle.Fill, AutoSize = true, Text = "Enter a search text", Margin = new Padding(2, 0, 0, 0) };
            _statusDefaultColor = _statusLabel.ForeColor;

            root.Controls.Add(optionsRow, 0, 0);
            root.Controls.Add(actionsRow, 0, 1);
            root.Controls.Add(_split, 0, 2);
            root.Controls.Add(_statusLabel, 0, 3);
            Controls.Add(root);

            _regexToggle.CheckedChanged += (s, e) => { _dotToggle.Enabled = _regexToggle.Checked; _wordsToggle.Enabled = !_regexToggle.Checked; };
            _dotToggle.Enabled = false;
            _searchBox.TextChanged += (s, e) => ScheduleHighlight();
            foreach (CheckBox toggle in new[] { _regexToggle, _dotToggle, _caseToggle, _wordsToggle })
                toggle.CheckedChanged += (s, e) => ScheduleHighlight();
            _highlightToggle.CheckedChanged += (s, e) => { if (_highlightToggle.Checked) ScheduleHighlight(); else Guarded(ClearHighlights); };
            ResumeLayout(true);
        }

        private static CheckBox MakeToggle(string text, string tip)
        {
            var cb = new CheckBox
            {
                Text = text,
                Appearance = Appearance.Button,
                AutoSize = true,
                MinimumSize = new Size(54, 24),
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = new Padding(0, 0, 3, 0),
                TabStop = false,
            };
            new ToolTip().SetToolTip(cb, tip);
            return cb;
        }

        private static Button MakeButton(string text, string shortcut, EventHandler onClick)
        {
            var b = new Button { Text = text, AutoSize = true, MinimumSize = new Size(78, 24), Margin = new Padding(0, 0, 3, 0), TabStop = false };
            new ToolTip().SetToolTip(b, shortcut);
            b.Click += onClick;
            return b;
        }

        private MultilineTextBox MakeBox()
        {
            var box = new MultilineTextBox { Dock = DockStyle.Fill, Font = new Font(FontFamily.GenericMonospace, 10f) };
            box.KeyDown += Box_KeyDown;
            return box;
        }

        /// <summary>
        /// FormBase wires generic Enter/Escape handlers to every control. The two text boxes handle their own
        /// keys in Box_KeyDown, so skip them to avoid a second newline on Enter.
        /// </summary>
        protected override void AddKeyUpDownPressHandlers(Control ctrl = null)
        {
            if (ctrl == null)
                ctrl = this;
            if (ctrl is MultilineTextBox)
                return;
            ctrl.KeyUp += (sender, e) => NppFormHelper.GenericKeyUpHandler(this, sender, e, IsModal);
            ctrl.KeyDown += NppFormHelper.GenericKeyDownHandler;
            if (ctrl.HasChildren)
                foreach (Control child in ctrl.Controls)
                    AddKeyUpDownPressHandlers(child);
        }

        protected override void OnDockingPanelClosed()
        {
            Guarded(ClearHighlights);
            Main.OnPanelClosedByUser();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible)
                ScheduleHighlight();
            else
                Guarded(ClearHighlights);
        }

        #endregion

        #region highlighting

        private void ScheduleHighlight()
        {
            if (!HighlightEnabled)
                return;
            _highlightTimer.Stop();
            _highlightTimer.Start();
        }

        private void RefreshHighlight()
        {
            Guarded(() =>
            {
                if (!HighlightEnabled || !Visible || Main.isShuttingDown)
                    return;
                HighlightResult r = _highlighter.Apply(SearchText, CurrentOptions);
                if (r.IndicatorUnavailable) SetStatus("Highlighting unavailable (no free indicator)", true);
                else if (r.TooLarge) SetStatus("Document too large (over 2 GB)", true);
                else if (r.RegexError) SetStatus("Invalid regular expression", true);
                else if (r.Capped) SetStatus("Highlight capped at " + r.Count + " matches", false);
                else if (string.IsNullOrEmpty(SearchText)) SetStatus("Enter a search text", false);
                else SetStatus(r.Count + " matches", false);
            });
        }

        public void ClearHighlights()
        {
            _highlightTimer.Stop();
            _highlighter.ClearCurrent();
        }

        /// <summary>Another document became active: repaint it, or clean it up if we painted it earlier.</summary>
        public void OnBufferActivated(IntPtr bufferId)
        {
            Guarded(() =>
            {
                if (HighlightEnabled && Visible && !string.IsNullOrEmpty(SearchText))
                    ScheduleHighlight();
                else if (_highlighter.IsTouched(bufferId))
                    _highlighter.ClearCurrent();
            });
        }

        public void OnFileClosed(IntPtr bufferId)
        {
            _highlighter.Forget(bufferId);
        }

        public void OnDocumentModified(int modificationType)
        {
            if (_suppressModifiedNotifications || !HighlightEnabled || !Visible)
                return;
            if ((modificationType & (ScModInsertText | ScModDeleteText)) != 0)
                ScheduleHighlight();
        }

        #endregion

        #region keys

        private void Box_KeyDown(object sender, KeyEventArgs e)
        {
            var box = (MultilineTextBox)sender;
            bool handled = true;
            switch (e.KeyCode)
            {
                case Keys.Enter:
                    if (e.Control && e.Shift) DoFindPrevious();
                    else if (e.Control) DoFindNext();
                    else box.InsertAtCaret("\r\n");
                    break;
                case Keys.Tab:
                    (box == _searchBox ? _replaceBox : _searchBox).Focus();
                    break;
                case Keys.Escape:
                    Npp.editor.GrabFocus();
                    break;
                case Keys.R when e.Control && e.Shift:
                    DoReplaceAll();
                    break;
                case Keys.R when e.Control:
                    DoReplace();
                    break;
                case Keys.C when e.Control && e.Shift:
                    DoCount();
                    break;
                case Keys.A when e.Control:
                    box.SelectAll();
                    break;
                default:
                    handled = false;
                    break;
            }
            if (handled)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        #endregion

        #region actions

        private void DoFindNext() => Guarded(() => ReportFind(_engine.FindNext(SearchText, CurrentOptions)));

        private void DoFindPrevious() => Guarded(() => ReportFind(_engine.FindPrevious(SearchText, CurrentOptions)));

        private void DoReplace() => Guarded(() =>
        {
            ReplaceResult r = _engine.Replace(SearchText, ReplaceText, CurrentOptions);
            ReportFind(r.Next, r.Replaced ? "Replaced; " : "");
        });

        private void DoReplaceAll() => Guarded(() =>
        {
            _suppressModifiedNotifications = true;
            ReplaceAllResult r;
            try
            {
                r = _engine.ReplaceAll(SearchText, ReplaceText, CurrentOptions);
            }
            finally
            {
                _suppressModifiedNotifications = false;
            }
            ScheduleHighlight();
            if (r.EmptyPattern) SetStatus("Enter a search text", false);
            else if (r.TooLarge) SetStatus("Document too large (over 2 GB)", true);
            else if (r.RegexError) SetStatus("Invalid regular expression", true);
            else SetStatus("Replaced " + r.Count, false);
        });

        private void DoCount() => Guarded(() =>
        {
            CountResult r = _engine.Count(SearchText, CurrentOptions, 1000000);
            if (r.EmptyPattern) SetStatus("Enter a search text", false);
            else if (r.TooLarge) SetStatus("Document too large (over 2 GB)", true);
            else if (r.RegexError) SetStatus("Invalid regular expression", true);
            else SetStatus(r.Count + " matches" + (r.Capped ? " (or more)" : ""), false);
        });

        private void ReportFind(SearchResult r, string prefix = "")
        {
            if (r.EmptyPattern) SetStatus("Enter a search text", false);
            else if (r.TooLarge) SetStatus("Document too large (over 2 GB)", true);
            else if (r.RegexError) SetStatus("Invalid regular expression", true);
            else if (!r.Found) SetStatus(prefix + "No more matches", false);
            else if (r.Wrapped) SetStatus(prefix + "Wrapped (line " + (r.Line + 1) + ")", false);
            else SetStatus(prefix + "Match found (line " + (r.Line + 1) + ")", false);
        }

        private void SetStatus(string text, bool isError)
        {
            _statusLabel.Text = text;
            _statusLabel.ForeColor = isError ? Color.FromArgb(StatusErrorColor) : _statusDefaultColor;
        }

        /// <summary>Never let an exception escape into Notepad++; show it once and log it.</summary>
        private void Guarded(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                SetStatus("Error: " + ex.Message, true);
                try
                {
                    Directory.CreateDirectory(Main.PluginConfigDirectory);
                    File.AppendAllText(Path.Combine(Main.PluginConfigDirectory, "error.log"),
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + ex + Environment.NewLine);
                }
                catch (Exception)
                {
                    // nothing more to do
                }
            }
        }

        #endregion

        #region settings

        private void LoadOptionsFromSettings()
        {
            Settings s = Main.settings;
            _regexToggle.Checked = s.use_regex;
            _dotToggle.Checked = s.dot_matches_newline;
            _caseToggle.Checked = s.match_case;
            _wordsToggle.Checked = s.whole_word;
            _loopToggle.Checked = s.wrap_around;
            _highlightToggle.Checked = s.highlight_matches;
            _dotToggle.Enabled = s.use_regex;
            _wordsToggle.Enabled = !s.use_regex;
        }

        public void SaveOptionsToSettings()
        {
            Settings s = Main.settings;
            s.use_regex = _regexToggle.Checked;
            s.dot_matches_newline = _dotToggle.Checked;
            s.match_case = _caseToggle.Checked;
            s.whole_word = _wordsToggle.Checked;
            s.wrap_around = _loopToggle.Checked;
            s.highlight_matches = _highlightToggle.Checked;
        }

        #endregion
    }
}
