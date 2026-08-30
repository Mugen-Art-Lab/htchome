using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace HTCHome.Manager
{
    internal static class ResumeSystemDiagnostics
    {
        private static readonly object Sync = new object();
        private static bool started;
        private static int generation;
        private static DateTime? lastSuspendUtc;
        private static string logPath;

        private static readonly string[] InterestingSystemProviders =
        {
            "display",
            "nvlddmkm",
            "dxgkrnl",
            "dwm",
            "desktop window manager",
            "kernel-power",
            "power-troubleshooter",
            "kernel-pnp",
            "userpnp",
            "driverframeworks",
            "whea"
        };

        private static readonly string[] ExtraChannels =
        {
            "Microsoft-Windows-DxgKrnl/Operational",
            "Microsoft-Windows-Dwm-Core/Operational",
            "Microsoft-Windows-Diagnostics-Performance/Operational",
            "Microsoft-Windows-DriverFrameworks-UserMode/Operational",
            "Microsoft-Windows-Kernel-PnP/Configuration"
        };

        public static void Start()
        {
            lock (Sync)
            {
                if (started) return;
                started = true;

                string logs = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
                Directory.CreateDirectory(logs);
                logPath = Path.Combine(logs, "manager-resume-system.log");
            }

            SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;
            SystemEvents.DisplaySettingsChanging += SystemEvents_DisplaySettingsChanging;
            SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;

            Write("=== MANAGER RESUME BLACK BOX START pid=" + Process.GetCurrentProcess().Id +
                  " utc=" + DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) + " ===");
            QueueSnapshot("START", 0, false);
        }

        public static void Stop()
        {
            lock (Sync)
            {
                if (!started) return;
                started = false;
            }

            try { SystemEvents.PowerModeChanged -= SystemEvents_PowerModeChanged; } catch { }
            try { SystemEvents.DisplaySettingsChanging -= SystemEvents_DisplaySettingsChanging; } catch { }
            try { SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged; } catch { }
            Write("=== MANAGER RESUME BLACK BOX STOP utc=" + DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) + " ===");
        }

        private static void SystemEvents_DisplaySettingsChanging(object sender, EventArgs e)
        {
            Write("DISPLAY_SETTINGS_CHANGING utc=" + DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
        }

        private static void SystemEvents_DisplaySettingsChanged(object sender, EventArgs e)
        {
            Write("DISPLAY_SETTINGS_CHANGED utc=" + DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            QueueSnapshot("DISPLAY_CHANGED", generation, false);
        }

        private static void SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Suspend)
            {
                lastSuspendUtc = DateTime.UtcNow;
                Write("POWER Suspend utc=" + lastSuspendUtc.Value.ToString("o", CultureInfo.InvariantCulture) +
                      " generation=" + generation);
                QueueSnapshot("SUSPEND", generation, true);
                return;
            }

            if (e.Mode != PowerModes.Resume)
            {
                Write("POWER " + e.Mode + " utc=" + DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) +
                      " generation=" + generation);
                return;
            }

            int current;
            lock (Sync)
            {
                generation++;
                current = generation;
            }

            DateTime resumeUtc = DateTime.UtcNow;
            Write("POWER Resume utc=" + resumeUtc.ToString("o", CultureInfo.InvariantCulture) +
                  " generation=" + current +
                  " suspendUtc=" + (lastSuspendUtc.HasValue
                      ? lastSuspendUtc.Value.ToString("o", CultureInfo.InvariantCulture)
                      : "<unknown>"));

            QueueSnapshot("RESUME+0s", current, true);
            QueueDelayedSnapshot("RESUME+2s", current, 2000, false);
            QueueDelayedSnapshot("RESUME+5s", current, 5000, true);
            QueueDelayedSnapshot("RESUME+10s", current, 10000, false);
            QueueDelayedSnapshot("RESUME+20s", current, 20000, true);
            QueueDelayedSnapshot("RESUME+30s", current, 30000, true);
        }

        private static void QueueDelayedSnapshot(string label, int currentGeneration, int delayMs, bool includeEvents)
        {
            Thread t = new Thread(delegate()
            {
                Thread.Sleep(delayMs);
                QueueSnapshot(label, currentGeneration, includeEvents);
            });
            t.IsBackground = true;
            t.Name = "HTC Home Mugen resume black box " + label;
            t.Start();
        }

        private static void QueueSnapshot(string label, int currentGeneration, bool includeEvents)
        {
            Thread t = new Thread(delegate()
            {
                try
                {
                    CaptureSnapshot(label, currentGeneration, includeEvents);
                }
                catch (Exception ex)
                {
                    Write("SNAPSHOT_FAILED label=" + label + " type=" + ex.GetType().FullName + " " + OneLine(ex.Message));
                }
            });
            t.IsBackground = true;
            t.Name = "HTC Home Mugen system snapshot " + label;
            t.Start();
        }

        private static void CaptureSnapshot(string label, int currentGeneration, bool includeEvents)
        {
            DateTime nowUtc = DateTime.UtcNow;
            Write("--- SNAPSHOT " + label +
                  " generation=" + currentGeneration +
                  " utc=" + nowUtc.ToString("o", CultureInfo.InvariantCulture) + " ---");

            CaptureScreens(label);
            CaptureVideoControllers(label);
            CaptureDisplayPnp(label);

            if (includeEvents)
            {
                DateTime sinceUtc = lastSuspendUtc.HasValue
                    ? lastSuspendUtc.Value.AddSeconds(-20)
                    : nowUtc.AddMinutes(-2);
                CaptureEventLog("System", sinceUtc, true, label);
                CaptureEventLog("Application", sinceUtc, true, label);

                foreach (string channel in ExtraChannels)
                    CaptureEventLog(channel, sinceUtc, false, label);
            }

            Write("--- END SNAPSHOT " + label + " generation=" + currentGeneration + " ---");
        }

        private static void CaptureScreens(string label)
        {
            try
            {
                Screen[] screens = Screen.AllScreens;
                Write("SCREENS label=" + label + " count=" + screens.Length);
                for (int i = 0; i < screens.Length; i++)
                {
                    Screen s = screens[i];
                    Write("SCREEN label=" + label +
                          " i=" + i +
                          " device=" + s.DeviceName +
                          " primary=" + s.Primary +
                          " bounds=" + Rect(s.Bounds.Left, s.Bounds.Top, s.Bounds.Width, s.Bounds.Height) +
                          " work=" + Rect(s.WorkingArea.Left, s.WorkingArea.Top, s.WorkingArea.Width, s.WorkingArea.Height) +
                          " bpp=" + s.BitsPerPixel);
                }
            }
            catch (Exception ex)
            {
                Write("SCREENS_FAILED label=" + label + " " + ex.GetType().Name + " " + OneLine(ex.Message));
            }
        }

        private static void CaptureVideoControllers(string label)
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    "SELECT Name,DriverVersion,PNPDeviceID,Status,AdapterRAM,CurrentHorizontalResolution,CurrentVerticalResolution,CurrentRefreshRate,VideoModeDescription,Availability,ConfigManagerErrorCode FROM Win32_VideoController"))
                using (ManagementObjectCollection results = searcher.Get())
                {
                    foreach (ManagementObject mo in results)
                    {
                        Write("GPU label=" + label +
                              " name=" + V(mo, "Name") +
                              " driver=" + V(mo, "DriverVersion") +
                              " status=" + V(mo, "Status") +
                              " availability=" + V(mo, "Availability") +
                              " cfgErr=" + V(mo, "ConfigManagerErrorCode") +
                              " pnp=" + V(mo, "PNPDeviceID") +
                              " adapterRam=" + V(mo, "AdapterRAM") +
                              " mode=" + V(mo, "VideoModeDescription") +
                              " res=" + V(mo, "CurrentHorizontalResolution") + "x" + V(mo, "CurrentVerticalResolution") +
                              " hz=" + V(mo, "CurrentRefreshRate"));
                    }
                }
            }
            catch (Exception ex)
            {
                Write("GPU_WMI_FAILED label=" + label + " " + ex.GetType().Name + " " + OneLine(ex.Message));
            }
        }

        private static void CaptureDisplayPnp(string label)
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    "SELECT Name,PNPDeviceID,Status,ConfigManagerErrorCode,Manufacturer FROM Win32_PnPEntity WHERE PNPClass='Display'"))
                using (ManagementObjectCollection results = searcher.Get())
                {
                    foreach (ManagementObject mo in results)
                    {
                        Write("DISPLAY_PNP label=" + label +
                              " name=" + V(mo, "Name") +
                              " status=" + V(mo, "Status") +
                              " cfgErr=" + V(mo, "ConfigManagerErrorCode") +
                              " maker=" + V(mo, "Manufacturer") +
                              " pnp=" + V(mo, "PNPDeviceID"));
                    }
                }
            }
            catch (Exception ex)
            {
                Write("DISPLAY_PNP_FAILED label=" + label + " " + ex.GetType().Name + " " + OneLine(ex.Message));
            }
        }

        private static void CaptureEventLog(string logName, DateTime sinceUtc, bool filterProviders, string label)
        {
            int written = 0;
            int scanned = 0;
            try
            {
                EventLogQuery query = new EventLogQuery(logName, PathType.LogName, "*");
                query.ReverseDirection = true;

                using (EventLogReader reader = new EventLogReader(query))
                {
                    EventRecord record;
                    while ((record = reader.ReadEvent()) != null)
                    {
                        using (record)
                        {
                            scanned++;
                            DateTime? created = record.TimeCreated;
                            if (created.HasValue && created.Value.ToUniversalTime() < sinceUtc)
                                break;

                            if (filterProviders && !IsInterestingProvider(record.ProviderName))
                            {
                                if (scanned >= 600) break;
                                continue;
                            }

                            string description = string.Empty;
                            try { description = record.FormatDescription(); } catch { }

                            Write("EVENT label=" + label +
                                  " log=" + logName +
                                  " utc=" + (created.HasValue ? created.Value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture) : "<null>") +
                                  " provider=" + OneLine(record.ProviderName) +
                                  " id=" + record.Id +
                                  " level=" + OneLine(record.LevelDisplayName) +
                                  " recordId=" + (record.RecordId.HasValue ? record.RecordId.Value.ToString(CultureInfo.InvariantCulture) : "<null>") +
                                  " msg=" + Truncate(OneLine(description), 900));
                            written++;

                            if (written >= 200 || scanned >= 600)
                                break;
                        }
                    }
                }

                Write("EVENT_SCAN label=" + label + " log=" + logName + " scanned=" + scanned + " written=" + written);
            }
            catch (Exception ex)
            {
                Write("EVENT_CHANNEL_UNAVAILABLE label=" + label +
                      " log=" + logName +
                      " type=" + ex.GetType().Name +
                      " msg=" + OneLine(ex.Message));
            }
        }

        private static bool IsInterestingProvider(string provider)
        {
            string p = (provider ?? string.Empty).ToLowerInvariant();
            return InterestingSystemProviders.Any(x => p.Contains(x));
        }

        private static string V(ManagementBaseObject mo, string property)
        {
            try
            {
                object value = mo[property];
                return value == null ? "<null>" : OneLine(Convert.ToString(value, CultureInfo.InvariantCulture));
            }
            catch
            {
                return "<error>";
            }
        }

        private static string Rect(int x, int y, int width, int height)
        {
            return x + "," + y + "," + width + "x" + height;
        }

        private static string Truncate(string value, int max)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= max) return value ?? string.Empty;
            return value.Substring(0, max) + "...";
        }

        private static string OneLine(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ").Trim();
        }

        private static void Write(string text)
        {
            try
            {
                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture) + " " + text;
                lock (Sync)
                {
                    if (string.IsNullOrEmpty(logPath))
                    {
                        string logs = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
                        Directory.CreateDirectory(logs);
                        logPath = Path.Combine(logs, "manager-resume-system.log");
                    }
                    File.AppendAllText(logPath, line + Environment.NewLine, new UTF8Encoding(false));
                }
            }
            catch
            {
            }
        }
    }
}
