using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Kbg.NppPluginNET.PluginInfrastructure;
using NppSearchReplace.Forms;
using NppSearchReplace.Utils;

namespace Kbg.NppPluginNET
{
    class Main
    {
        internal const string PluginName = "NppSearchReplace";
        public static readonly string PluginConfigDirectory = Path.Combine(Npp.notepad.GetConfigDirectory(), PluginName);
        public static Settings settings = new Settings();
        public static SearchPanel searchPanel = null;
        public static bool isShuttingDown = false;
        internal const int IdShowPanel = 0;

        static internal void CommandMenuInit()
        {
            // dependent assemblies (NppSearchReplace.Core.dll) live next to the plugin, not next to notepad++.exe
            AppDomain.CurrentDomain.AssemblyResolve += LoadDependency;
            Translator.ResetTranslations(true);

            PluginBase.SetCommand(IdShowPanel, "Show Search && Replace Panel", ToggleSearchPanel, new ShortcutKey(true, true, false, Keys.F));
            PluginBase.SetCommand(1, "---", null);
            PluginBase.SetCommand(2, "&Settings", OpenSettings);
            PluginBase.SetCommand(3, "&About", ShowAbout);
        }

        private static Assembly LoadDependency(object sender, ResolveEventArgs args)
        {
            string assemblyFile = Path.Combine(Npp.pluginDllDirectory, new AssemblyName(args.Name).Name) + ".dll";
            if (File.Exists(assemblyFile))
                return Assembly.LoadFrom(assemblyFile);
            return null;
        }

        static internal void SetToolBarIcons()
        {
            // no toolbar icons in v1
        }

        public static void OnNotification(ScNotification notification)
        {
            uint code = notification.Header.Code;
            switch (code)
            {
            case (uint)NppMsg.NPPN_READY:
                if (settings.panel_visible)
                {
                    ShowSearchPanel();
                    Npp.editor.GrabFocus();
                }
                break;
            case (uint)NppMsg.NPPN_BUFFERACTIVATED:
                Npp.editor = new ScintillaGateway(PluginBase.GetCurrentScintilla());
                searchPanel?.OnBufferActivated(notification.Header.IdFrom);
                break;
            case (uint)NppMsg.NPPN_FILECLOSED:
                searchPanel?.OnFileClosed(notification.Header.IdFrom);
                break;
            case (uint)SciMsg.SCN_MODIFIED:
                searchPanel?.OnDocumentModified(notification.ModificationType);
                break;
            case (uint)NppMsg.NPPN_WORDSTYLESUPDATED:
                RestyleEverything();
                break;
            case (uint)NppMsg.NPPN_NATIVELANGCHANGED:
                Translator.ResetTranslations(false);
                break;
            }
        }

        static internal void PluginCleanUp()
        {
            isShuttingDown = true;
            if (searchPanel != null && !searchPanel.IsDisposed)
                searchPanel.SaveOptionsToSettings();
            settings.SaveToIniFile();
            if (searchPanel != null && !searchPanel.IsDisposed)
            {
                searchPanel.ClearHighlights();
                searchPanel.Close();
                searchPanel.Dispose();
            }
        }

        public static void RestyleEverything()
        {
            if (searchPanel != null && !searchPanel.IsDisposed)
                FormStyle.ApplyStyle(searchPanel, settings.use_npp_styling);
        }

        static void OpenSettings()
        {
            settings.ShowDialog();
        }

        static void ShowAbout()
        {
            MessageBox.Show(
                $"NppSearchReplace v{Npp.AssemblyVersionString()}\r\n" +
                "Docked multiline search and replace panel for Notepad++.\r\n" +
                $"Notepad++ {Npp.nppVersionStr}",
                "About NppSearchReplace", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public static void ToggleSearchPanel()
        {
            bool visible = searchPanel != null && !searchPanel.IsDisposed && searchPanel.Visible;
            if (visible)
                HideSearchPanel();
            else
                ShowSearchPanel();
        }

        public static void ShowSearchPanel()
        {
            if (searchPanel == null || searchPanel.IsDisposed)
            {
                searchPanel = new SearchPanel();
                RegisterDockingPanel(searchPanel);
            }
            Npp.notepad.ShowDockingForm(searchPanel);
            settings.panel_visible = true;
            SetPanelMenuCheck(true);
            string selection = Npp.editor.GetSelText();
            bool singleLine = selection.IndexOf('\n') < 0 && selection.IndexOf('\r') < 0;
            searchPanel.FocusSearchBox(singleLine ? selection : null);
        }

        public static void HideSearchPanel()
        {
            if (searchPanel != null && !searchPanel.IsDisposed)
            {
                searchPanel.ClearHighlights();
                Npp.notepad.HideDockingForm(searchPanel);
            }
            settings.panel_visible = false;
            SetPanelMenuCheck(false);
        }

        /// <summary>Called by the panel when the user closes it with the docking X button.</summary>
        public static void OnPanelClosedByUser()
        {
            settings.panel_visible = false;
            SetPanelMenuCheck(false);
        }

        private static void SetPanelMenuCheck(bool isChecked)
        {
            Win32.SendMessage(PluginBase.nppData._nppHandle, (uint)NppMsg.NPPM_SETMENUITEMCHECK,
                PluginBase._funcItems.Items[IdShowPanel]._cmdID, isChecked ? 1 : 0);
        }

        private static void RegisterDockingPanel(Form form)
        {
            var data = new NppTbData
            {
                hClient = form.Handle,
                pszName = "Search & Replace",
                dlgID = IdShowPanel,
                uMask = NppTbMsg.DWS_DF_CONT_BOTTOM,
                pszModuleName = PluginName, // same convention as the template; Notepad++ uses it to remember the panel's dock position
            };
            IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf(data));
            Marshal.StructureToPtr(data, ptr, false);
            Win32.SendMessage(PluginBase.nppData._nppHandle, (uint)NppMsg.NPPM_DMMREGASDCKDLG, 0, ptr);
            // Notepad++ keeps a pointer to this struct, so it is intentionally never freed.
        }
    }
}
