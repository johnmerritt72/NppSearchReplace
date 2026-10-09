using System.ComponentModel;
using CsvQuery.PluginInfrastructure;
using Kbg.NppPluginNET;

namespace NppSearchReplace.Utils
{
    /// <summary>
    /// Persisted in %AppData%\Notepad++\plugins\config\NppSearchReplace\NppSearchReplace.ini by SettingsBase.
    /// Property names are the INI keys, so keep them stable.
    /// </summary>
    public class Settings : SettingsBase
    {
        public override void OnSettingsChanged()
        {
            base.OnSettingsChanged();
            Main.RestyleEverything();
        }

        #region SEARCH
        [Description("Use regular expressions"), Category("Search"), DefaultValue(false)]
        public bool use_regex { get; set; }

        [Description("'.' matches newline (regex only)"), Category("Search"), DefaultValue(false)]
        public bool dot_matches_newline { get; set; }

        [Description("Match case"), Category("Search"), DefaultValue(false)]
        public bool match_case { get; set; }

        [Description("Whole word only (ignored when regex is on)"), Category("Search"), DefaultValue(false)]
        public bool whole_word { get; set; }

        [Description("Wrap around at the end or start of the document"), Category("Search"), DefaultValue(true)]
        public bool wrap_around { get; set; }

        [Description("Highlight all matches in the active document while typing"), Category("Search"), DefaultValue(false)]
        public bool highlight_matches { get; set; }

        [Description("Maximum number of matches to highlight"), Category("Search"), DefaultValue(20000)]
        public int highlight_cap { get; set; }
        #endregion

        #region PANEL
        [Description("Width of the search box as a percentage of the panel (10-90)"), Category("Panel"), DefaultValue(50)]
        public int splitter_percent { get; set; }

        [Description("Re-open the panel at startup if it was open when Notepad++ closed"), Category("Panel"), DefaultValue(false)]
        public bool panel_visible { get; set; }
        #endregion

        #region STYLING
        [Description("Use the same colors as the editor window for this plugin's forms?"), Category("Styling"), DefaultValue(true)]
        public bool use_npp_styling { get; set; }
        #endregion
    }
}
