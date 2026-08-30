using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows;

namespace HTCHome.Manager
{
    public partial class App : Application
    {
        private Mutex instanceMutex;
        private EventWaitHandle activateEvent;
        private Thread activateThread;
        private bool ownsMutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            string suffix = GetInstallationId();
            string mutexName = @"Local\HTCHome.Mugen.Manager." + suffix;
            string eventName = @"Local\HTCHome.Mugen.Manager.Activate." + suffix;

            bool createdNew;
            instanceMutex = new Mutex(true, mutexName, out createdNew);
            ownsMutex = createdNew;

            if (!createdNew)
            {
                SignalExistingInstance(eventName);
                Shutdown();
                return;
            }

            bool eventCreated;
            activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, eventName, out eventCreated);
            activateThread = new Thread(ActivationLoop)
            {
                IsBackground = true,
                Name = "HTC Home Mugen Manager activation listener"
            };
            activateThread.Start();

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                if (activateEvent != null)
                {
                    activateEvent.Close();
                    activateEvent = null;
                }
            }
            catch { }

            try
            {
                if (instanceMutex != null)
                {
                    if (ownsMutex) instanceMutex.ReleaseMutex();
                    instanceMutex.Close();
                    instanceMutex = null;
                }
            }
            catch { }

            base.OnExit(e);
        }

        private void ActivationLoop()
        {
            while (true)
            {
                try
                {
                    activateEvent.WaitOne();
                }
                catch
                {
                    return;
                }

                try
                {
                    Dispatcher.BeginInvoke(new Action(BringManagerToFront));
                }
                catch
                {
                    return;
                }
            }
        }

        private void BringManagerToFront()
        {
            var window = Windows.OfType<MainWindow>().FirstOrDefault();
            if (window == null) return;

            window.ShowInTaskbar = true;
            window.Show();
            if (window.WindowState == WindowState.Minimized)
                window.WindowState = WindowState.Normal;

            window.Activate();
            window.Topmost = true;
            window.Topmost = false;
            window.Focus();
        }

        private static void SignalExistingInstance(string eventName)
        {
            // The first instance creates the activation event immediately after
            // acquiring the mutex. Retry briefly to cover a very fast double-click.
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    using (EventWaitHandle existing = EventWaitHandle.OpenExisting(eventName))
                    {
                        existing.Set();
                        return;
                    }
                }
                catch (WaitHandleCannotBeOpenedException)
                {
                    Thread.Sleep(50);
                }
                catch
                {
                    return;
                }
            }
        }

        private static string GetInstallationId()
        {
            string path = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .ToUpperInvariant();

            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(path));
                return BitConverter.ToString(hash, 0, 8).Replace("-", string.Empty);
            }
        }
    }
}
