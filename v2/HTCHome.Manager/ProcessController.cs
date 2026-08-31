using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;

namespace HTCHome.Manager
{
    internal sealed class ProcessController
    {
        private const uint WM_CLOSE = 0x0010;
        private readonly string rootDirectory;
        private readonly string executablePath;

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        public ProcessController(string rootDirectory)
        {
            this.rootDirectory = rootDirectory;
            executablePath = Path.GetFullPath(Path.Combine(rootDirectory, "HTCHome.exe"));
        }

        public void Start(ProfileRecord profile)
        {
            if (profile == null) return;

            string executable = Path.Combine(rootDirectory, "HTCHome.exe");
            if (!File.Exists(executable))
                throw new FileNotFoundException(ManagerText.ExecutableNotFound, executable);

            string arguments = "--profile " + profile.Id;
            string diagnosticMode = profile.EffectiveResumeDiagnosticMode;
            if (!string.Equals(diagnosticMode, "normal", StringComparison.OrdinalIgnoreCase))
                arguments += " --resume-diag " + diagnosticMode;

            Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                Arguments = arguments,
                WorkingDirectory = rootDirectory,
                UseShellExecute = true
            });
        }

        public void Stop(ProfileRecord profile)
        {
            if (profile == null) return;

            foreach (Process process in FindProcesses(profile.Id))
            {
                try
                {
                    bool closeRequested = RequestGracefulClose(process);
                    if (closeRequested)
                    {
                        try
                        {
                            if (process.WaitForExit(3000))
                                continue;
                        }
                        catch { }
                    }

                    try { process.Kill(); } catch { }
                }
                finally
                {
                    process.Dispose();
                }
            }
        }

        private static bool RequestGracefulClose(Process process)
        {
            if (process == null) return false;

            bool sent = false;
            int processId;
            try { processId = process.Id; }
            catch { return false; }

            try
            {
                process.Refresh();
                IntPtr mainWindow = process.MainWindowHandle;
                if (mainWindow != IntPtr.Zero)
                    sent |= PostMessage(mainWindow, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            }
            catch { }

            try
            {
                EnumWindows(delegate(IntPtr hWnd, IntPtr lParam)
                {
                    uint ownerPid;
                    GetWindowThreadProcessId(hWnd, out ownerPid);
                    if (ownerPid == (uint)processId)
                        sent |= PostMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                    return true;
                }, IntPtr.Zero);
            }
            catch { }

            return sent;
        }

        public HashSet<string> GetRunningProfileIds()
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using (var searcher = new ManagementObjectSearcher(
                "SELECT CommandLine, ExecutablePath FROM Win32_Process WHERE Name = 'HTCHome.exe'"))
            {
                foreach (ManagementObject item in searcher.Get())
                {
                    string commandLine = item["CommandLine"] as string;
                    string processPath = item["ExecutablePath"] as string;
                    if (string.IsNullOrEmpty(commandLine) || string.IsNullOrEmpty(processPath)) continue;

                    string fullPath;
                    try { fullPath = Path.GetFullPath(processPath); } catch { continue; }
                    if (!fullPath.Equals(executablePath, StringComparison.OrdinalIgnoreCase)) continue;

                    string profileId = ExtractProfileId(commandLine);
                    if (!string.IsNullOrWhiteSpace(profileId)) ids.Add(profileId);
                }
            }

            return ids;
        }

        private Process[] FindProcesses(string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId)) return new Process[0];
            var found = new List<Process>();

            using (var searcher = new ManagementObjectSearcher(
                "SELECT ProcessId, CommandLine, ExecutablePath FROM Win32_Process WHERE Name = 'HTCHome.exe'"))
            {
                foreach (ManagementObject item in searcher.Get())
                {
                    string commandLine = item["CommandLine"] as string;
                    string processPath = item["ExecutablePath"] as string;
                    if (string.IsNullOrEmpty(commandLine) || string.IsNullOrEmpty(processPath)) continue;

                    string fullPath;
                    try { fullPath = Path.GetFullPath(processPath); } catch { continue; }
                    if (!fullPath.Equals(executablePath, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!string.Equals(ExtractProfileId(commandLine), profileId, StringComparison.OrdinalIgnoreCase)) continue;

                    int pid = Convert.ToInt32((uint)item["ProcessId"]);
                    try { found.Add(Process.GetProcessById(pid)); } catch { }
                }
            }

            return found.ToArray();
        }

        private static string ExtractProfileId(string commandLine)
        {
            if (string.IsNullOrWhiteSpace(commandLine)) return null;
            string[] markers = { "--profile=", "--profile " };
            foreach (string marker in markers)
            {
                int index = commandLine.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (index < 0) continue;
                string tail = commandLine.Substring(index + marker.Length).TrimStart();
                if (tail.StartsWith("\""))
                {
                    int endQuote = tail.IndexOf('"', 1);
                    return endQuote > 1 ? tail.Substring(1, endQuote - 1) : tail.Trim('"');
                }
                int end = tail.IndexOfAny(new[] { ' ', '\t', '\r', '\n' });
                return end >= 0 ? tail.Substring(0, end) : tail;
            }
            return null;
        }
    }
}
