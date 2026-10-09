using System;
using System.Windows.Forms;

namespace NppSearchReplace.Forms
{
    /// <summary>
    /// Notepad++ runs IsDialogMessage over registered modeless dialogs, which turns Enter into the default
    /// button and Tab into focus movement before the text box ever sees them. Answering WM_GETDLGCODE with
    /// DLGC_WANTALLKEYS makes every key reach this control as a normal KeyDown, so the panel decides what
    /// Enter, Tab and Escape do.
    /// </summary>
    public class MultilineTextBox : TextBox
    {
        private const int WM_GETDLGCODE = 0x0087;
        private const int DLGC_WANTARROWS = 0x0001;
        private const int DLGC_WANTTAB = 0x0002;
        private const int DLGC_WANTALLKEYS = 0x0004;
        private const int DLGC_HASSETSEL = 0x0008;
        private const int DLGC_WANTCHARS = 0x0080;

        public MultilineTextBox()
        {
            Multiline = true;
            AcceptsReturn = true;
            AcceptsTab = true;
            WordWrap = false;
            ScrollBars = ScrollBars.Both;
            HideSelection = false;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_GETDLGCODE)
            {
                m.Result = new IntPtr(DLGC_WANTALLKEYS | DLGC_WANTARROWS | DLGC_WANTTAB | DLGC_WANTCHARS | DLGC_HASSETSEL);
                return;
            }
            base.WndProc(ref m);
        }

        /// <summary>Keep WinForms from treating Tab/Enter/Escape as dialog keys; KeyDown handles them.</summary>
        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Tab:
                case Keys.Enter:
                case Keys.Escape:
                    return true;
            }
            return base.IsInputKey(keyData);
        }

        /// <summary>Insert text at the caret, replacing any selection, and keep the caret after it.</summary>
        public void InsertAtCaret(string text)
        {
            int start = SelectionStart;
            SelectedText = text;
            SelectionStart = start + text.Length;
            SelectionLength = 0;
            ScrollToCaret();
        }
    }
}
