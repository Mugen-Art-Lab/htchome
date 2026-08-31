# HTC Home Mugen — latest resume investigation result

Updated: 2026-08-31 after the four-way composition matrix reproduced a bad wake.

Read this together with `docs/MUGEN_RESUME_INVESTIGATION.md`.

## Decisive four-way matrix result

The same four long-lived HTC Home profile processes were assigned one suspend-time presentation mode each:

```text
TV              -> Baseline (no change)
Монитор слева   -> WPF Hide
Монитор справа  -> DWM Cloak
Основной монитор -> Minimize
```

A healthy wake first established that all four matrix modes execute and restore correctly. In particular, `DWM Cloak` returned `hr=0` while the WPF Window remained `IsVisible=True`, `WindowState=Normal`, and non-iconic. This is an important control: Cloak removes the HWND from DWM presentation without asking WPF to hide or minimize its Window.

The next reproduced bad wake split the matrix cleanly:

```text
Baseline / TV          -> poisoned MediaSystem, widget frozen
WPF Hide / left        -> healthy, fresh MediaContext OK, widget restored
DWM Cloak / right      -> poisoned MediaSystem, fresh MediaContext OOM
Minimize / main        -> healthy, fresh MediaContext OK, widget restored
```

Both poisoned profiles failed in the familiar process-local WPF composition path:

```text
System.OutOfMemoryException
   at System.Windows.Media.Composition.DUCE.Channel.SyncFlush()
   at System.Windows.Media.MediaContext.CompleteRender()
   at System.Windows.Media.MediaContext.CreateChannels()
   at System.Windows.Media.MediaSystem.ConnectChannels(MediaContext mc)
```

The Hide and Minimize controls could create a fresh STA Dispatcher / MediaContext / HwndSource on the exact same bad system wake and returned to working UI with the original HWNDs.

The Cloak result is especially discriminating. `DwmSetWindowAttribute(DWMWA_CLOAK)` succeeded, the HWND stayed alive, and DWM was told not to present it, but WPF still considered the Window visible, normal, and non-minimized. That process acquired the same poisoned MediaSystem state as Baseline. Therefore simply removing the HWND from visible DWM presentation is **not sufficient** to prevent the failure.

The right/Cloak process later disappeared from Manager around the restore interval. Its log proves the earlier fresh-MediaContext OOM, but the final terminating stack was not captured, so the exact cause of process exit remains probable rather than proven.

## Current interpretation

The strongest discriminator is now not user-visible pixels or DWM visibility by itself. It is something in the WPF presentation state changed by both `Window.Hide()` and `WindowState=Minimized`, but **not** changed by DWM Cloak.

A leading target is the existing WPF `HwndTarget` state machine around the late display reconfiguration after resume. Across many runs Windows emits a burst of `DisplaySettingsChanging/Changed` roughly +9 to +12 seconds after `PowerModes.Resume`, even when all four display devices remain enumerated and the NVIDIA adapter reports healthy status. On at least one wake a display work area was observed transiently as `0,0,0x0` during that burst before returning to normal.

This is consistent with the older failure stacks involving `HwndTarget.UpdateWindowSettings` / `UpdateWindowPos` and DUCE synchronization, but the exact causal state transition is not yet proven.

`Tier 0` remains only an observation, not the definition of the bug. Healthy Hide/Minimize controls can initially resume at Tier 0 and then create a fresh Tier-2 MediaContext. The reliable poisoned-state marker remains inability to create a fresh MediaContext/HwndSource in the same PID with the DUCE OOM stack.

## Next diagnostic: passive HwndTarget state timeline

The next build keeps the four-way matrix behavior unchanged and adds a passive `ResumeHwndTargetStateProbe`.

It records the existing HwndTarget private fields already used in earlier investigation:

```text
_isSuspended
_needsRePresentOnWake
_hasRePresentedSinceWake
_isRenderTargetEnabled
_disableCookie
_isMinimized
_isSessionDisconnected
_lastWakeOrUnlockEvent
```

Snapshots are recorded for:

- the cached healthy state immediately before Suspend intervention;
- the live state immediately after the matrix applies Hide/Cloak/Minimize;
- Resume +0, +250 ms, +1 s, +3 s, +10 s, +12 s, +21 s, +24 s, and +30 s;
- every `DisplaySettingsChanging` and `DisplaySettingsChanged` notification;
- unhandled AppDomain exceptions and process exit.

The probe does not change HwndTarget state or add another recovery mechanism. Its goal is to identify the exact field/state transition shared by Hide + Minimize but absent from Baseline + Cloak.

If such a discriminator is found, the following experiment should manipulate only that narrow WPF presentation state while leaving the Window visible and normal. That is the path toward a root-cause fix rather than an automated hide/restart workaround.

## Earlier decisive hidden-profile result — generation 11

Before the four-way matrix, one profile was hidden with `Window.Hide()` while three peers remained visible. On the same bad wake all three visible processes acquired the MediaSystem/DUCE failure while the hidden profile survived, created a fresh MediaContext, and returned with the same HWND. The hidden Mugen Manager also remained healthy.

That experiment established that preventing one HTC Home process from normal visible WPF presentation during the vulnerable wake could change the outcome without restarting or rebuilding the process.

## Earlier decisive fresh-Dispatcher result

Before the hidden-profile experiment, all four visible HTC Home processes were taken through consecutive hibernate/resume cycles. On a bad generation all four fresh STA Dispatcher probes failed before creating their off-screen HwndSource with the same `DUCE.Channel.SyncFlush -> MediaContext.CreateChannels -> MediaSystem.ConnectChannels` OOM stack.

That established that once an HTC Home PID is poisoned, replacing only Window/HwndTarget/Dispatcher state inside that same process is not a viable post-failure recovery boundary.

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
- DWM Cloak / non-presentation by itself as sufficient protection
- Tier 0 by itself as a sufficient definition of the poisoned state
