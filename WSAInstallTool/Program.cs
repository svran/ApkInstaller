using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WSAInstallTool.Util;

namespace WSAInstallTool
{
    static class Program
    {
        public const string ARG_INSTALL_ASSOC = "--install-assoc";
        public const string ARG_UNINSTALL_ASSOC = "--uninstall-assoc";

        [STAThread]
        static void Main(String[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            IniUtil.Instance.Init();
            LangUtil.Instance.Init();
            PreferenceUtil.Instance.UpdateBlackListByTime();

            if (args.Length == 0)
            {
                Application.Run(new MainForm());
            }
            else if (args[0] == ARG_INSTALL_ASSOC || args[0] == ARG_UNINSTALL_ASSOC)
            {
                Application.Run(new MainForm(args));
            }
            else
            {
                Application.Run(new InstallForm(args));
            }
        }

        public static bool IsAdministrator()
        {
            WindowsIdentity current = WindowsIdentity.GetCurrent();
            WindowsPrincipal windowsPrincipal = new WindowsPrincipal(current);
            return windowsPrincipal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        public static void RestartElevated(string action)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = Process.GetCurrentProcess().MainModule.FileName,
                Arguments = action,
                Verb = "runas"
            };
            try
            {
                Process.Start(startInfo);
            }
            catch (Win32Exception)
            {
                return;
            }
            Application.Exit();
        }
    }
}
