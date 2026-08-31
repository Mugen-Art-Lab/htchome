# HTC Home Mugen — latest resume investigation result

Updated: 2026-08-31 after analysis of `Logs(20260831-014859).zip`.

Read this together with `docs/MUGEN_RESUME_INVESTIGATION.md`.

## Decisive hidden-profile control result — generation 11

The hidden-profile experiment finally reproduced the real failure.

Four long-lived `HTCHome.exe` profile processes went through the same hibernate/resume event. Three remained visible at suspend; the `Основной монитор` profile was the only `--resume-hide-control` process and was synchronously hidden at suspend without restarting or recreating its HWND.

Suspend/control setup:

```text
07:44:51 [ResumeControl] SUSPEND_HIDE_BEGIN ... tier=2
07:44:51 [ResumeControl] HIDE_WINDOW ... hwnd=0x581F3C
07:44:51 [ResumeControl] SUSPEND_HIDE_OK hiddenWindows=1 ... tier=2
```

The machine resumed at about 07:48:38. Around 11 seconds later Windows emitted the familiar burst of `DisplaySettingsChanging/Changed` notifications for the four-display topology.

### Three visible HTC Home processes — poisoned

`TV`, `Монитор слева`, and `Монитор справа` all returned with `mainTier=0`, stayed at Tier 0, and their fresh STA Dispatcher probes all failed in the same process-local WPF composition path:

```text
[ResumeProbe] RESUME generation=11 ... mainTier=0
[ResumeProbe] NEW_DISPATCHER_PROBE_OOM generation=11
System.OutOfMemoryException
   at System.Windows.Media.Composition.DUCE.Channel.SyncFlush()
   at System.Windows.Media.MediaContext.CompleteRender()
   at System.Windows.Media.MediaContext.CreateChannels()
   at System.Windows.Media.MediaSystem.ConnectChannels(MediaContext mc)
```

Their original widget windows remained visible and kept the same HWNDs, but the processes were the familiar frozen/bad-resume state.

### Hidden main-monitor control — healthy on the same bad wake

The hidden `Основной монитор` process saw the same system wake and the same display-topology change burst, but did **not** acquire the poisoned MediaSystem state:

```text
07:48:38 [ResumeProbe] RESUME generation=11 ... mainTier=0
07:48:38 [ResumeControl] RESUME_HOLD_HIDDEN generation=11 hiddenWindows=1 mainTier=0
07:48:50 [ResumeProbe] NEW_DISPATCHER_BEGIN generation=11 ... tierBeforeSource=2
07:48:52 [ResumeProbe] NEW_DISPATCHER_SOURCE_OK generation=11 ... tier=2 renderMode=Default
07:48:58 [ResumeProbe] NEW_DISPATCHER_PROBE_OK generation=11 ... tier=2
07:49:00 [ResumeProbe] NEW_DISPATCHER_END generation=11
07:49:00 [ResumeControl] RESTORE_QUEUE generation=11 hiddenWindows=1 tier=2
07:49:01 [ResumeControl] RESTORE_OK generation=11 restoredWindows=1 ... tier=2
```

At +30 seconds the control process remained healthy at Tier 2 and the restored widget was visibly working.

Crucially, the control window kept the **same HWND `0x581F3C` before hide and after restore**. `Window.Hide()` did not replace the window or restart/rebuild the process. The important difference was that this WPF window was not visible/participating in composition during the vulnerable suspend/resume/display-reconfiguration interval.

The Mugen Manager also remained healthy on generation 11: its hidden WPF process stayed Tier 2 and its own fresh Dispatcher/HwndSource probe succeeded.

## Interpretation after generation 11

This is the strongest evidence so far that **visible WPF composition participation across suspend/resume is a necessary trigger, or is extremely close to the trigger, for the HTC Home process-local MediaSystem/DUCE corruption on this machine**.

The bad system wake was shared by all four profiles, yet:

```text
visible HTC Home x3  -> MediaSystem/DUCE OOM, frozen
hidden HTC Home x1   -> fresh MediaContext OK, restored and working
hidden Manager       -> fresh MediaContext OK
```

This sharply weakens explanations based only on global GPU state, RAM pressure, profile content, weather animation, or a machine-wide WPF failure. The failing state is still process-local after it occurs, but preventing a WPF window from being visible during the vulnerable wake prevented that process from entering the poisoned state in this controlled run.

`Tier 0` is now clearly **not sufficient to diagnose the bug**. The protected control itself resumed at `mainTier=0`, yet its new Dispatcher immediately obtained Tier 2 and rendered successfully. The reliable bad-state marker remains failure to create a new `MediaContext` / `HwndSource` with the DUCE OOM stack.

## Next engineering direction

Do not spend more time attempting post-failure Dispatcher/HwndTarget reconstruction inside a poisoned PID.

Work should now focus on a preventative resume strategy:

1. remove visible HTC Home WPF windows from composition before suspend;
2. keep them out through the late post-resume display-topology transition (observed roughly +10 to +12 seconds on these runs);
3. restore them only after the display state has stabilized;
4. reduce or mask the visible blank interval so the workaround is acceptable as product behavior.

The current diagnostic uses a deliberately conservative fixed +22 second restore delay. That delay is useful for proving the hypothesis but is not acceptable final UX. A practical next prototype should restore after display changes settle and/or after a healthy WPF composition probe succeeds, rather than waiting a fixed 22 seconds.

A swapped-control repeat on a different physical monitor/profile would be useful as an additional causal confirmation, but generation 11 is already a highly discriminating result because three visible peers failed simultaneously while the single hidden peer survived the exact same bad wake.

## Earlier decisive #42 result

Before the hidden-profile experiment, the same four `HTCHome.exe` profile processes were tested through two consecutive hibernate/resume cycles without being restarted between cycles.

On a healthy generation all four original widget UIs resumed normally at Tier 2 and the diagnostic fresh STA Dispatcher probe succeeded.

On the next bad generation all four visible widgets froze, all four returned at Tier 0, and all four fresh Dispatcher probes failed before creating their off-screen `HwndSource` with the same `DUCE.Channel.SyncFlush -> MediaContext.CreateChannels -> MediaSystem.ConnectChannels` OOM stack.

That established that once an HTC Home PID is poisoned, replacing only Window/HwndTarget/Dispatcher state inside that same process is not a viable recovery boundary.

## What is already ruled out as a necessary cause

- literal RAM exhaustion
- right-click/context menu as the trigger
- NVIDIA overlay handle leak as the only cause
- weather animation / cloud Storyboards
- `AllowsTransparency=True` / layered windows
- existing HwndTarget hardware rendering mode
- switching HwndTarget to SoftwareOnly before suspend
- switching HwndTarget to SoftwareOnly after resume
- creating a fresh HWND/HwndTarget on the old Dispatcher after failure
- creating a fresh Dispatcher/MediaContext inside the same poisoned process after failure
- a machine-wide failure that necessarily poisons every WPF process on the bad wake
- Tier 0 by itself as a sufficient definition of the poisoned state
