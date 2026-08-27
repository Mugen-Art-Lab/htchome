using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;

namespace HTCHome.Manager
{
    internal sealed class ProcessController
    {
        private readonly string rootDirectory;

        public ProcessController(string rootDirectory)
        {
            this.rootDirectory = rootDirectory;
        }

        public void Start(ProfileRecord profile)
        {
            if (profile == null || IsRunning(profile.Id)) return;

            string executable = Path.Combine(rootDirectory, "HTCHome.exe");
            if (!File.Exists(executable))
                throw new FileNotFoundException("HTCHome.exe not found next to Manager.", executable);

            Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                Arguments = "--profile " + profile.Id,
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
                    if (!process.CloseMainWindow()) process.Kill();
                }
                catch
                {
                    try { process.Kill(); } catch { }
                }
            }
        }

        public bool IsRunning(string profileId)
        {
            return FindProcesses(profileId).Any();
        }

        private Process[] FindProcesses(string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId)) return new Process[0];
            var found = new System.Collections.Generic.List<Process>();

            using (var searcher = new ManagementObjectSearcher(
                "SELECT ProcessId, CommandLine, ExecutablePath FROM Win32_Process WHERE Name = 'HTCHome.exe'"))
            {
                foreach (ManagementObject item in searcher.Get())
                {
                    string commandLine = item["CommandLine"] as string;
                    string executablePath = item["ExecutablePath"] as string;
                    if (string.IsNullOrEmpty(commandLine) || string.IsNullOrEmpty(executablePath)) continue;
                    if (!Path.GetFullPath(executablePath).Equals(Path.GetFullPath(Path.Combine(rootDirectory, "HTCHome.exe")), StringComparison.OrdinalIgnoreCase)) continue;
                    if (commandLine.IndexOf("--profile " + profileId, StringComparison.OrdinalIgnoreCase) < 0 &&
                        commandLine.IndexOf("--profile=" + profileId, StringComparison.OrdinalIgnoreCase) < 0) continue;

                    int pid = Convert.ToInt32((uint)item["ProcessId"]);
                    try { found.Add(Process.GetProcessById(pid)); } catch { }
                }
            }

            return found.ToArray();
        }
    }
}
