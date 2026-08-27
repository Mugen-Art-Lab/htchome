using System;
using System.IO;
using Microsoft.Win32;

namespace HTCHome.Manager
{
    internal sealed class AutostartController
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "HTC Home Mugen Manager";
        private readonly string command;

        public AutostartController(string rootDirectory)
        {
            command = "\"" + Path.Combine(rootDirectory, "HTCHome.Manager.exe") + "\" --autostart";
        }

        public bool IsEnabled()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    return key != null && !string.IsNullOrWhiteSpace(key.GetValue(ValueName) as string);
                }
            }
            catch
            {
                return false;
            }
        }

        public void SetEnabled(bool enabled)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(RunKeyPath))
            {
                if (enabled)
                    key.SetValue(ValueName, command, RegistryValueKind.String);
                else
                    key.DeleteValue(ValueName, false);
            }
        }
    }
}
