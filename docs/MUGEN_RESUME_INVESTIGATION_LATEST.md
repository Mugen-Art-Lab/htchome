# HTC Home Mugen — latest resume investigation result

Updated: 2026-08-30 after analysis of `Logs(20260830-171614).zip`, collected after workflow run #42.

Read this together with `docs/MUGEN_RESUME_INVESTIGATION.md`.

## Earlier decisive #42 result

The same four `HTCHome.exe` profile processes were tested through two consecutive hibernate/resume cycles without being restarted between cycles.

### Generation 1 — healthy resume

All four original widget UIs resumed normally. The main WPF render tier remained `tier=2`.

The diagnostic STA-thread probe succeeded in every process:

```text
[ResumeProbe] NEW_DISPATCHER_BEGIN generation=1 ... tierBeforeSource=2
[ResumeProbe] NEW_DISPATCHER_SOURCE_OK generation=1 ... tier=2 renderMode=Default
[ResumeProbe] NEW_DISPATCHER_PROBE_OK generation=1 ...
[ResumeProbe] NEW_DISPATCHER_END generation=1
```

This established that the probe itself is valid when the process graphics state is healthy.

### Generation 2 — bad resume

On the next hibernate/resume, all four original widgets froze. All four processes returned with `mainTier=0` and remained `tier=0` through the +30 second diagnostic snapshots.

The fresh STA Dispatcher probe failed in **all four processes** before it could create its off-screen `HwndSource`.

Each process logged the same stack:

```text
[ResumeProbe] NEW_DISPATCHER_PROBE_OOM generation=2
System.OutOfMemoryException: Недостаточно памяти для продолжения выполнения программы.
   at System.Windows.Media.Composition.DUCE.Channel.SyncFlush()
   at System.Windows.Media.MediaContext.CompleteRender()
   at System.Windows.Media.MediaContext.CreateChannels()
   at System.Windows.Media.MediaSystem.ConnectChannels(MediaContext mc)
   at System.Windows.Media.MediaContext..ctor(Dispatcher dispatcher)
   at System.Windows.Media.MediaContext.From(Dispatcher dispatcher)
   at HTCHome.ResumeRenderRecovery...RunFreshDispatcherProbe...
```

The failure occurs while WPF is constructing the new `MediaContext`, specifically while `MediaSystem.ConnectChannels` / `MediaContext.CreateChannels` tries to connect the new Dispatcher to the WPF composition system.

No fresh `HwndSource` is created in the bad-resume case.

## New control-process evidence from Logs(20260830-171614)

The later log bundle adds an important control process: **HTC Home Mugen Manager itself**.

On the same bad wake where all four visible `HTCHome.exe` profile processes changed from `tier=2` to `tier=0` and their fresh Dispatcher probes failed with the DUCE/MediaSystem OOM, the Manager process remained healthy:

```text
HTCHome.exe x4: mainTier=0, fresh MediaContext/HwndSource probe -> OOM
HTCHome.Manager.exe: tier=2, fresh Dispatcher + HwndSource probe -> OK
```

The Manager probe succeeds on both the healthy wake and the bad wake. Therefore the shared graphics wake event does **not** poison arbitrary WPF processes system-wide.

The damage is still process-wide *inside each affected HTC Home PID*: once an HTC Home process is poisoned, a fresh Dispatcher/MediaContext inside that same PID cannot reconnect to WPF composition. But another WPF process can remain healthy through the same wake.

A useful visible-state difference in this bundle is:

```text
HTCHome.exe profile processes: visible=True before suspend
HTCHome.Manager.exe:           visible=0 before suspend
```

This does not prove visibility is the cause, but it makes an active visible/rendering WPF window at suspend/resume a strong discriminator worth testing directly.

## Current interpretation

The practical post-failure recovery boundary remains the **process boundary**, not Window, HwndTarget, or Dispatcher. Automatic process restart is still not accepted as the final product fix.

The investigation target is now narrower than the earlier note suggested:

- a common system graphics/display wake event still appears to trigger the failure;
- the failure is not a global WPF failure across every process;
- affected HTC Home processes acquire poisoned process-local/native WPF MediaSystem state;
- a hidden Manager process can survive the same event with Tier 2 and create a new MediaContext;
- therefore window visibility/render participation at the suspend boundary is now a concrete candidate.

`Tier 0` remains an indicator rather than a proven root cause. Previous tests showed a visually working instance can exist at Tier 0. In the decisive healthy-vs-bad pairs, however, Tier 2 vs Tier 0 correlates with whether the process MediaSystem can create a new MediaContext.

## What is already ruled out as a necessary cause

- literal RAM exhaustion
- right-click/context menu as the trigger
- NVIDIA overlay handle leak as the only cause
- weather animation / cloud Storyboards
- `AllowsTransparency=True` / layered windows
- existing HwndTarget hardware rendering mode
- switching HwndTarget to SoftwareOnly before suspend
- switching HwndTarget to SoftwareOnly after resume
- creating a fresh HWND/HwndTarget on the old Dispatcher
- creating a fresh Dispatcher/MediaContext inside the same poisoned process after the bad resume
- a machine-wide failure that necessarily poisons every WPF process on the bad wake (Manager survives)

## Next controlled experiment: one hidden HTC Home profile

The Manager now supports selecting exactly one profile as a resume diagnostic control. When that profile is launched, Manager adds:

```text
--resume-hide-control
```

Only that HTC Home process reacts to the flag.

At `PowerModes.Suspend` it synchronously asks its WPF UI Dispatcher to hide all currently visible process windows, records their HWND/type/title, and keeps the process alive. It does **not** restart the process, close the windows, or intentionally recreate their HWNDs.

After resume the control process remains hidden while the existing fresh-Dispatcher probe runs at +12 seconds for 6 seconds. At +22 seconds it queues restoration of the hidden windows on the original UI Dispatcher.

Expected diagnostic log markers:

```text
[ResumeControl] ENABLED ...
[ResumeControl] SUSPEND_HIDE_BEGIN ...
[ResumeControl] HIDE_WINDOW ...
[ResumeControl] SUSPEND_HIDE_OK ...
[ResumeControl] RESUME_HOLD_HIDDEN ...
[ResumeProbe] NEW_DISPATCHER_...
[ResumeControl] RESTORE_QUEUE ...
[ResumeControl] RESTORE_OK ...
```

### Decisive outcomes

**If the three visible HTC Home processes fail with Tier 0 / MediaSystem OOM while the hidden control stays Tier 2 and its fresh Dispatcher probe succeeds:** visibility/render participation at the suspend boundary becomes strongly implicated. The next experiment should distinguish simple visibility from actual HwndTarget/composition attachment.

**If the hidden control fails exactly like the three visible processes:** merely hiding the existing WPF windows at Suspend is not sufficient. The next experiment should move the boundary earlier/stronger, for example starting one control profile hidden from process launch or explicitly removing/recreating its presentation target around suspend.

Either result is useful and avoids spending more time on post-resume Dispatcher reconstruction that has already been disproven as a recovery mechanism.
