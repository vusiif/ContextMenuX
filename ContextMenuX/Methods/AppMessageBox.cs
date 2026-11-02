using ContextMenuX.Methods;
using System.Windows.Forms;

namespace ContextMenuX.Methods
{
    public static class AppMessageBox
    {
        public static DialogResult Show(string text, MessageBoxButtons buttons = MessageBoxButtons.OK,
            MessageBoxIcon icon = MessageBoxIcon.Warning, string caption = null)
        {
            return MessageBoxEx.Show(text, caption ?? AppString.General.AppName, buttons, icon);
        }
    }
}