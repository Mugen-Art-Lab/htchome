using System;

namespace HTCHome
{
    // Intentionally inert in the non-layered-window A/B build.
    // The existing ResumeDiagnostics remains active and records power, display,
    // WPF tier, process and window state. Previous experiments proved that
    // changing RenderMode or creating another HwndTarget in the same process
    // cannot recover a poisoned DUCE/MediaContext channel, so this layer must not
    // touch rendering during the AllowsTransparency experiment.
    internal static class ResumeRenderRecovery
    {
        public static bool Start()
        {
            // For this diagnostic run only, keep DWM blur/glass out of the test.
            // Do not Save(): the user's normal setting must remain unchanged.
            try
            {
                HTCHome.Properties.Settings.Default.EnableGlass = false;
                App.Log("[ResumeProbe] NON_LAYERED_AB AllowsTransparency=False EnableGlass=False (runtime only)");
            }
            catch
            {
            }

            return true;
        }
    }

    public partial class App
    {
        private static readonly bool ResumeRenderRecoveryBootstrap = ResumeRenderRecovery.Start();
    }
}
