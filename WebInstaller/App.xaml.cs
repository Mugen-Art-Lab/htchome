using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;
using System.IO;
using System.Reflection;
using System.Globalization;
using System.Windows.Markup;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace WebInstaller
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        [DllImport("shell32.dll")]
        public static extern IntPtr ShellExecute(IntPtr hwnd,
                                                 string lpOperation,
                                                 string lpFile,
                                                 string lpParameters,
                                                 string lpDirectory,
                                                 int nShowCmd);

        public static string Path = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            Log("Unhandled exception!");
            Log(e.Exception.ToString());
            MessageBox.Show(e.Exception.ToString());
            e.Handled = true;
        }

        public static void Log(string message)
        {
            if (!File.Exists(App.Path + "\\log.txt"))
            {
                File.WriteAllText(App.Path + "\\log.txt", string.Empty);
            }

            try
            {
                File.AppendAllText(App.Path + "\\log.txt",
                   DateTime.Now + " -------------- " + message + (char)(13) + (char)(10));
            }
            catch (Exception ex)
            {
                MessageBox.Show("Can't write log. " + ex.Message);
            }
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            if (e.Args.Contains("/skip"))
                E.SkipStartPage = true;

            if (e.Args.Contains("/desktop"))
                E.DesktopShortcut = true;
            else
                E.DesktopShortcut = false;

            if (e.Args.Contains("/startmenu"))
                E.StartMenuShortcut = true;
            else
                E.StartMenuShortcut = false;

            if (e.Args.Length > 0)
            {
                var path = from x in e.Args where x.Contains(@":\") select x;
                if (path != null)
                {
                    E.InstallPath = path.First();
                }
            }

            if (!Assembly.GetExecutingAssembly().Location.EndsWith("Uninstall.exe"))
            {
                InstallerWindow w = new InstallerWindow();
                w.Show();
            }
            else
            {
                Path = Directory.GetParent(Path).FullName;
                UninstallWindow w = new UninstallWindow();
                w.Show();
            }
        }
    }
}
