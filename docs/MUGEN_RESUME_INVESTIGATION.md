# HTC Home Mugen — hibernate/resume investigation notes

Last updated: 2026-08-30
Branch: `mugen/profile-instances`
Current diagnostic build: workflow run **#42** (`fea9a0947bb4ccf91f2234ac761d540b34329601`)

## Purpose

This file is the continuity note for the long-running HTC Home Mugen investigation into freezes and false `OutOfMemoryException` errors after Windows hibernate/resume. It is intentionally detailed so work can continue from a new chat/session without repeating already completed experiments.

## User-visible failure

Classic HTC Home can survive normal use for long periods, but after hibernation/resume one or more widget processes may freeze visually. A later interaction (for example right-click, close, move, context menu, or a compositor sync) often exposes:

```text
System.OutOfMemoryException: Недостаточно памяти для продолжения выполнения программы.
```

The error is **not literal RAM exhaustion**. Dumps and process metrics showed ample free physical memory, modest private bytes, and no general memory pressure in the affected `HTCHome.exe` processes.

Common WPF stacks include:

```text
System.Windows.Media.Composition.DUCE.Channel.SyncFlush()
System.Windows.Media.MediaContext.NotifyChannelMessage()
System.Windows.Interop.HwndTarget.UpdateWindowSettings()
System.Windows.Interop.HwndTarget.UpdateWindowPos()
```

The most important recent reproduction showed all four profile processes throwing the same OOM almost simultaneously during the wake cycle, before the normal `SystemEvents.PowerModeChanged=Resume` notification.

## Architecture / test setup

HTC Home Mugen runs one shared installation with multiple profile instances:

```text
HTCHome.exe --profile <id>
```

A separate `HTCHome.Manager.exe` starts/stops profiles and manages autostart/tray behavior.

Four profile instances are used during resume testing. The display topology is stable enough for saved coordinates; no forced monitor rebinding is part of the current test.

### Manager work already completed

Implemented and tested:

- arbitrary profile names and stable profile IDs
- start/stop one or all profiles
- rename/delete profiles
- per-profile autostart
- Manager autostart and tray mode
- RU/EN language switching
- persistent Manager placement/settings
- graceful widget stop (`WM_CLOSE`, wait for save, kill only as fallback)
- single Manager instance per installation
- smoother process-status refresh
- profile-aware logging
- NVIDIA compatibility diagnostics and FrameView exclusions UI

Relevant commits include:

```text
a65c178a  fix(v2): make startup temp probe multi-instance safe and profile log errors
b080c360  feat(manager): add bilingual NVIDIA compatibility diagnostics
b20695fd  fix(manager): enforce one manager instance per installation
57eac988  fix(manager): stretch GridView cells for real centering
```

## Confirmed separate NVIDIA overlay problem

A distinct issue was identified earlier: when NVIDIA Overlay functionality was enabled, affected HTC Home processes accumulated huge numbers of NVIDIA IPC kernel objects.

In dumps, the handle divergence was almost entirely:

```text
Section  {2627E361-24E2-4F14-99ED-A20D0685D8DD}
Mutant   {2627E361-24E2-4F14-99ED-A20D0685D8DD}
```

`nvspcap64.dll` (NVIDIA capture/overlay component) was loaded in the processes.

Example comparison:

- one process ~9769 handles
- comparison process ~3571 handles
- difference ~6198 handles
- NVIDIA Section + Mutant difference ~6196 handles

Disabling NVIDIA Overlay globally stopped the runaway handle growth. Handles then stayed around ~1000 per instance across samples and at least one hibernate cycle.

Important: `nvspcap64.dll` can remain loaded even when overlay functionality is disabled, so “DLL loaded” does not mean the active overlay path is enabled.

This NVIDIA leak is real, but later tests with Overlay OFF and stable handles proved it is **not the only resume failure path**.

## Passive resume diagnostics

Commit:

```text
9e3b9a10  diag(v2): trace WPF resume and restore default renderer
```

Diagnostics record:

- `SystemEvents.PowerModeChanged`
- `DisplaySettingsChanging/Changed`
- key window messages (`WM_POWERBROADCAST`, `WM_DISPLAYCHANGE`, `WM_DEVICECHANGE`, `WM_DPICHANGED`, `WM_DWMCOMPOSITIONCHANGED`, `WM_WINDOWPOSCHANGING/CHANGED`)
- snapshots at resume +0ms / +250ms / +1s / +3s / +10s / +30s
- PID, handles, working set/private bytes/GC
- WPF render mode and `RenderCapability.Tier`
- window geometry and monitor mapping

### Important observation

With NVIDIA Overlay OFF, all four processes could enter hibernate at `tier=2` and return initially as `tier=0`. In one test tier stayed 0 through +30s while the UI was frozen.

However, later a non-layered TV instance remained visually usable even while `tier=0`, so **Tier 0 is not by itself the root cause**. It is best treated as a symptom or recovery-state indicator.

## BEFORE / AFTER dump result

A frozen process was dumped before interacting with it, then right-click was used to expose the familiar OOM, and the same PID was dumped again.

### BEFORE

- same process already visually frozen
- ~991 handles
- no runaway NVIDIA named objects
- modest working set/private bytes
- lots of free physical RAM
- WPF render threads waiting, no CPU spin

### AFTER

- same PID and module set
- handles only ~+22 (normal UI activity)
- private bytes only slightly higher
- managed OOM stack:

```text
DUCE.Channel.SyncFlush()
HwndTarget.UpdateWindowSettings()
HwndTarget.UpdateWindowPos()
HwndTarget.HandleMessage()
```

Conclusion: the right-click did **not** cause the corruption. It merely forced a compositor/render-target sync and surfaced an already-broken WPF graphics state.

## WPF HwndTarget internal state observation

Reflection diagnostics inspected fields such as:

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

One notable post-resume state was:

```text
_isSuspended=False
_needsRePresentOnWake=False
_hasRePresentedSinceWake=False
_isRenderTargetEnabled=True
_isMinimized=False
_isSessionDisconnected=False
```

Even tens of seconds after wake, `_hasRePresentedSinceWake=False` could remain while WPF otherwise believed the target was enabled and no re-present was pending.

## Experiments that did NOT fix the problem

### 1. Process-wide SoftwareOnly render mode

Mugen profile processes were temporarily forced to:

```csharp
System.Windows.Media.RenderOptions.ProcessRenderMode =
    System.Windows.Interop.RenderMode.SoftwareOnly;
```

Result: did not eliminate the root issue. It changed symptoms (silent freeze instead of immediately exposed OOM), but resume could still poison rendering.

### 2. Post-resume existing HwndTarget -> SoftwareOnly

Commit:

```text
3d754c3f  diag(v2): isolate post-resume HwndTarget recovery
```

After resume, if Tier stayed 0, the existing `HwndTarget.RenderMode` was switched from Default to SoftwareOnly and the same Window invalidated.

Result: `REBIND_OK` logged, render mode stayed SoftwareOnly, UI still froze.

Conclusion: switching render mode on the already-created target after resume is too late.

### 3. Pre-suspend HwndTarget -> SoftwareOnly

Commit:

```text
927ef862  diag(v2): arm software HwndTarget before suspend
```

Existing targets were synchronously switched to SoftwareOnly while still at Tier 2, before GPU powerdown.

Result: all four targets changed successfully before sleep, remained SoftwareOnly after wake, but visual freezes still occurred.

Conclusion: the problem is not simply hardware-vs-software `HwndTarget` mode.

### 4. Fresh HWND/HwndTarget on the SAME Dispatcher

Commit:

```text
496d9fcb  diag(v2): probe fresh HwndTarget after resume
```

A fresh duplicate WPF window/HWND/HwndTarget was created inside the same process after resume.

Result: fresh-window creation itself could fail with OOM / WPF source-window errors. A process could even show Tier 2 and still fail to create a healthy fresh target on the existing Dispatcher.

Conclusion: the damage is broader than one specific `HwndTarget`; the main suspicion moved to the Dispatcher-scoped WPF `MediaContext/DUCE` channel.

## `AllowsTransparency=False` A/B

A decisive non-layered test temporarily changed the main Widget from:

```xml
Background="Transparent"
AllowsTransparency="True"
```

to a normal non-layered WPF window:

```xml
Background="#FFF0F0F0"
AllowsTransparency="False"
```

Legacy DWM glass/blur was disabled for the A/B so the test would not become a hybrid.

### First broken A/B build

The first attempt accidentally emptied Storyboards while existing mouse handlers still indexed `Children[0]`. This caused:

```text
ArgumentOutOfRangeException
MS.Utility.FrugalStructList<T>.get_Item(Int32 index)
HTCHome.Widget.Window_MouseLeave(...)
```

That build was invalid for resume conclusions.

### Corrected non-layered A/B

The corrected build restored the normal Storyboards while keeping `AllowsTransparency=False` and DWM glass disabled.

Results were initially encouraging: several instances survived hibernate/resume and continued animating.

However, a later clean test with:

- all four fresh processes
- weather animation OFF on all four
- NVIDIA Overlay OFF
- non-layered windows
- stable ~1000 handles
- normal memory

still produced **simultaneous OOM in all four processes during wake**.

Therefore:

> `AllowsTransparency=True` / layered WPF is NOT a necessary condition for the failure.

Transparency may affect probability or manifestation, but it is not the root cause.

The normal transparent widget was restored after this conclusion.

## Weather animation hypothesis

Weather clouds use real WPF Storyboards/DoubleAnimations. For example `Cloud.xaml.cs` creates movement animations roughly 9–20 seconds long, restarts them across several cycles, and removes the cloud from the Canvas later.

One TV instance remained with weather animation enabled while the other three had it disabled, and that TV instance was found frozen after a resume. This raised suspicion that weather animation might increase composition load.

A later clean test disabled weather animation on **all four** fresh processes.

Result: all four still threw the wake-cycle DUCE/MediaContext OOM.

Therefore:

> weather Storyboards are NOT a necessary condition for the failure.

They may still influence timing/probability, but they are not the root cause.

## Strongest current conclusion

The current failure path is best described as:

```text
hibernate / graphics power-down / display + compositor reconstruction
    -> WPF render/composition channel enters invalid state
    -> main Dispatcher/MediaContext/DUCE path becomes poisoned
    -> widget may freeze before any user interaction
    -> later compositor sync or even WPF's own wake processing calls DUCE.Channel.SyncFlush
    -> false OutOfMemoryException
```

The most important recent clean reproduction showed:

- all four instances fresh
- NVIDIA overlay functionality disabled
- no runaway handles
- weather animation disabled everywhere
- non-layered windows (`AllowsTransparency=False`)
- normal RAM/private bytes
- all four entered sleep at Tier 2
- all four threw OOM almost simultaneously during the wake cycle
- stack centered on:

```text
DUCE.Channel.SyncFlush()
MediaContext.NotifyChannelMessage()
MediaContextNotificationWindow.MessageFilter()
```

- this happened **before** normal `SystemEvents.PowerModeChanged=Resume`

This rules out literal RAM exhaustion and makes per-widget UI activity a secondary factor, not the root trigger.

## Current experiment: fresh Dispatcher / MediaContext in same PID

Current commit/build:

```text
fea9a0947bb4ccf91f2234ac761d540b34329601
fix(v2): invalidate probe UIElement instead of Visual
GitHub Actions run #42 — successful
```

The preceding run #41 failed to compile because `InvalidateVisual()` was mistakenly called on base type `Visual`; #42 fixes that by invalidating the concrete `Border` (`UIElement`).

### What #42 does

The normal transparent widget is restored.

After `PowerModeChanged=Resume`, the diagnostic code waits ~12 seconds and then starts a **new STA thread**. That thread creates:

- a new WPF `Dispatcher`
- therefore a fresh Dispatcher-scoped WPF `MediaContext`
- a small off-screen `HwndSource`
- a simple animated `Border`/`TextBlock`
- a render-priority timer that invalidates the visual for ~6 seconds

The probe is deliberately independent of the main UI Dispatcher so it can still run even if the original UI Dispatcher/MediaContext is poisoned.

### Expected log outcomes

Healthy fresh Dispatcher:

```text
[ResumeProbe] NEW_DISPATCHER_BEGIN ...
[ResumeProbe] NEW_DISPATCHER_SOURCE_OK ...
[ResumeProbe] NEW_DISPATCHER_PROBE_OK ...
[ResumeProbe] NEW_DISPATCHER_END ...
```

If it receives the same graphics failure:

```text
[ResumeProbe] NEW_DISPATCHER_PROBE_OOM ...
```

or

```text
[ResumeProbe] NEW_DISPATCHER_UNHANDLED ...
```

### Interpretation

If `NEW_DISPATCHER_PROBE_OK` appears after the main widgets have frozen/failed:

> the damaged state is likely scoped to the original Dispatcher/MediaContext. A future recovery design might recreate the UI on a fresh STA Dispatcher without restarting the whole process.

If the new Dispatcher also gets the DUCE OOM:

> the damaged state is below Dispatcher scope (process-wide/native WPF graphics state). At that point a fresh process becomes the realistic recovery boundary; per-HwndTarget and per-Dispatcher recovery would be insufficient.

## Current test procedure for run #42

1. Stop all HTC Home profile instances and exit old Manager/processes if needed.
2. Replace the test files with the #42 artifact.
3. Launch Manager and Start all profiles.
4. Confirm all widgets look normal/transparent again.
5. Hibernate.
6. After resume, avoid unnecessary interaction for ~20 seconds even if widgets appear frozen or error dialogs appear.
7. Collect the per-profile logs.
8. Search for `NEW_DISPATCHER_` entries and compare with the normal `[ResumeDiag]` wake timeline.

## Things not to conclude again

These have already been tested and should not be re-proposed as primary fixes without new evidence:

- “The PC is out of RAM.” — false for observed failures.
- “Right-click causes the crash.” — false; it can merely expose an already broken render channel.
- “Tier 0 is the cause.” — false; Tier 0 can coexist with a visually working instance.
- “NVIDIA overlay handle leak explains everything.” — false; it is a real separate bug/path, but the WPF wake failure reproduces with overlay functionality disabled and stable handles.
- “Switch existing HwndTarget to SoftwareOnly after resume.” — tested, did not recover.
- “Switch existing HwndTarget to SoftwareOnly before suspend.” — tested, did not prevent failure.
- “Create a new HWND on the same Dispatcher.” — tested, fresh target can fail on the poisoned Dispatcher.
- “AllowsTransparency=True is required.” — false; clean non-layered build also reproduced the wake OOM.
- “Weather animation is required.” — false; all four processes reproduced with weather animation OFF.

## Relevant source areas

Main host:

```text
v2/HTC Home/App.xaml.cs
v2/HTC Home/Widget.xaml
v2/HTC Home/Widget.xaml.cs
v2/HTC Home/ResumeRenderRecovery.cs
v2/HTC Home/Properties/Settings.Designer.cs
```

Weather animation example:

```text
v2/Widgets/WeatherClockWidget/WeatherAnimation/Cloud.xaml
v2/Widgets/WeatherClockWidget/WeatherAnimation/Cloud.xaml.cs
```

Manager:

```text
v2/HTCHome.Manager/
```

## Future directions after #42 result

If the fresh Dispatcher survives:

- isolate a complete widget host on a dedicated STA Dispatcher
- test migration/recreation of the visual tree after wake
- preserve saved coordinates and classic appearance
- ensure plugin static state does not bind permanently to the original window/Dispatcher
- consider health heartbeat in Manager as fallback only

If the fresh Dispatcher also fails:

- treat the WPF native/process graphics state as poisoned after the specific resume failure
- investigate whether any supported process-level WPF graphics reset exists (likely limited)
- use targeted per-profile process replacement only as a last-resort recovery architecture
- minimize visible pop-to-front behavior if process replacement becomes unavoidable

Longer-term project direction remains: one install, many profiles, classic HTC Sense visual experience preserved, no forced redesign, no unnecessary process restart unless the evidence proves it is the real recovery boundary.
