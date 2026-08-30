# HTC Home Mugen — latest resume investigation result

Updated: 2026-08-30 after workflow run #42 (`fea9a0947bb4ccf91f2234ac761d540b34329601`).

Read this together with `docs/MUGEN_RESUME_INVESTIGATION.md`.

## Decisive #42 result

The same four `HTCHome.exe` profile processes were tested through two consecutive hibernate/resume cycles without being restarted between cycles.

### Generation 1 — healthy resume

All four original widget UIs resumed normally. The main WPF render tier remained `tier=2`.

The new diagnostic STA-thread probe succeeded in every process:

```text
[ResumeProbe] NEW_DISPATCHER_BEGIN generation=1 ... tierBeforeSource=2
[ResumeProbe] NEW_DISPATCHER_SOURCE_OK generation=1 ... tier=2 renderMode=Default
[ResumeProbe] NEW_DISPATCHER_PROBE_OK generation=1 ...
[ResumeProbe] NEW_DISPATCHER_END generation=1
```

This established that the probe itself is valid when the process graphics state is healthy.

### Generation 2 — bad resume

On the next hibernate/resume, all four original widgets froze. All four processes returned with `mainTier=0` and remained `tier=0` through the +30 second diagnostic snapshots.

The fresh STA Dispatcher probe then failed in **all four processes** before it could create its off-screen `HwndSource`.

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

## Interpretation

This rules out recovery by simply moving HTC Home to a new STA Dispatcher inside the same process.

The damaged state is **below Dispatcher scope**. A new Dispatcher receives the same DUCE/MediaSystem failure because the process-wide/native WPF graphics/composition system is already poisoned.

The practical recovery boundary is therefore currently the **process boundary**, not the Window, HwndTarget, or Dispatcher boundary.

This does **not** mean automatic process restart is accepted as the final product fix. The investigation should now shift from post-failure recovery to **preventing the process-wide WPF MediaSystem/DUCE corruption during hibernate/resume**.

## Useful same-process healthy-vs-bad comparison

For the main profile in this test:

Healthy generation 1:

```text
Suspend: tier=2
Resume generation=1: mainTier=2
+0ms/+250ms/+1s/+3s/+10s/+30s: tier=2
fresh Dispatcher: PROBE_OK
```

Bad generation 2:

```text
Suspend: tier=2
Resume generation=2: mainTier=0
+0ms/+250ms/+1s/+3s/+10s/+30s: tier=0
fresh Dispatcher: OOM in MediaContext.CreateChannels -> MediaSystem.ConnectChannels
```

The four processes fail synchronously, which strongly points to a shared system graphics/compositor/driver resume event rather than independent widget logic.

`Tier 0` remains an indicator, not proven root cause: previous tests showed a visually working instance can exist at Tier 0. In this exact healthy-vs-bad pair, however, Tier 2 vs Tier 0 cleanly correlates with whether the process MediaSystem is healthy enough to create a new MediaContext.

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
- creating a fresh Dispatcher/MediaContext inside the same process after the bad resume

## Next investigation target

Do not spend more time on post-resume Window/HwndTarget/Dispatcher reconstruction. The next useful work is to compare the **system-level graphics wake sequence** between a healthy resume and a bad resume and identify what poisons WPF `MediaSystem` before/while it reconnects its DUCE channels.

Useful next evidence should include, around the exact suspend/resume timestamps:

- Windows System/Application event log entries for display/GPU/DWM/driver recovery
- NVIDIA driver (`nvlddmkm`) / DXGKRNL / Desktop Window Manager events if present
- display topology and adapter state transitions
- potentially ETW/WPF graphics diagnostics if practical
- exact ordering of WPF tier changes relative to power/display notifications

The current logs already show the important boundary: once the bad wake has occurred, a new `MediaContext` in the same PID cannot connect to `MediaSystem` and immediately dies in `DUCE.Channel.SyncFlush()`.
