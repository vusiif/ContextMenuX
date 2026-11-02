using ContextMenuX.Methods;
using System;
using System.Windows.Forms;

namespace ContextMenuX
{
    //兼容.Net3.5和.Net4.0，兼容Vista - Win11
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if(SingleInstance.IsRunning()) return;
            AppString.LoadStrings();
            Updater.PeriodicUpdate();
            XmlDicHelper.ReloadDics();
            Application.Run(new MainForm());
        }
    }
}