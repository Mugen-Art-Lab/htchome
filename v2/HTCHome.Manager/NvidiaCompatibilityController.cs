using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;

namespace HTCHome.Manager
{
    internal sealed class NvidiaProcessSnapshot
    {
        public string ProfileId { get; set; }
        public string ProfileName { get; set; }
        public int? ProcessId { get; set; }
        public int Handles { get; set; }
        public bool NvidiaModuleLoaded { get; set; }
    }

    internal sealed class NvidiaCompatibilityController
    {
        private const string NvidiaModuleName = "nvspcap64.dll";
        private const string ExecutableName = "HTCHome.exe";
        private readonly string executablePath;

        public NvidiaCompatibilityController(string rootDirectory)
        {
            executablePath = Path.GetFullPath(Path.Combine(rootDirectory, ExecutableName));
        }

        public static string FrameViewDirectory
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "NVIDIA Corporation", "FrameView"); }
        }

        public static string OverlayExcludePath
        {
            get { return Path.Combine(FrameViewDirectory, "ExcludeList.overlay.txt"); }
        }

        public static string LoggingExcludePath
        {
            get { return Path.Combine(FrameViewDirectory, "ExcludeList.txt"); }
        }

        public IList<NvidiaProcessSnapshot> GetSnapshots(IEnumerable<ProfileRecord> profiles)
        {
            var rows = profiles.Select(p => new NvidiaProcessSnapshot
            {
                ProfileId = p.Id,
                ProfileName = p.Name
            }).ToDictionary(p => p.ProfileId, StringComparer.OrdinalIgnoreCase);

            using (var searcher = new ManagementObjectSearcher(
                "SELECT ProcessId, CommandLine, ExecutablePath FROM Win32_Process WHERE Name = 'HTCHome.exe'"))
            {
                foreach (ManagementObject item in searcher.Get())
                {
                    string commandLine = item["CommandLine"] as string;
                    string processPath = item["ExecutablePath"] as string;
                    if (string.IsNullOrWhiteSpace(commandLine) || string.IsNullOrWhiteSpace(processPath)) continue;

                    string fullPath;
                    try { fullPath = Path.GetFullPath(processPath); }
                    catch { continue; }
                    if (!fullPath.Equals(executablePath, StringComparison.OrdinalIgnoreCase)) continue;

                    string profileId = ExtractProfileId(commandLine);
                    NvidiaProcessSnapshot row;
                    if (string.IsNullOrWhiteSpace(profileId) || !rows.TryGetValue(profileId, out row)) continue;

                    int pid = Convert.ToInt32((uint)item["ProcessId"]);
                    row.ProcessId = pid;
                    try
                    {
                        using (Process process = Process.GetProcessById(pid))
                        {
                            row.Handles = process.HandleCount;
                            row.NvidiaModuleLoaded = HasNvidiaModule(process);
                        }
                    }
                    catch { }
                }
            }

            return rows.Values.OrderBy(p => p.ProfileName, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        public static bool AreFrameViewExclusionsPresent()
        {
            return ContainsExecutable(OverlayExcludePath) && ContainsExecutable(LoggingExcludePath);
        }

        public static void ApplyFrameViewExclusions()
        {
            Directory.CreateDirectory(FrameViewDirectory);
            EnsureExecutable(OverlayExcludePath);
            EnsureExecutable(LoggingExcludePath);
        }

        private static void EnsureExecutable(string path)
        {
            if (ContainsExecutable(path)) return;

            if (File.Exists(path))
            {
                string backup = path + ".mugen-backup";
                if (!File.Exists(backup)) File.Copy(path, backup, false);
            }

            File.AppendAllText(path, ExecutableName + Environment.NewLine);
        }

        private static bool ContainsExecutable(string path)
        {
            if (!File.Exists(path)) return false;
            try
            {
                return File.ReadAllLines(path).Any(line =>
                    string.Equals(line.Trim(), ExecutableName, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }

        private static bool HasNvidiaModule(Process process)
        {
            try
            {
                foreach (ProcessModule module in process.Modules)
                {
                    if (string.Equals(module.ModuleName, NvidiaModuleName, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            catch { }
            return false;
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
