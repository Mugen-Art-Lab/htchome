# HTC Home Mugen — latest resume investigation result

Updated: 2026-09-01 after run #55 exposed a healthy-process stale-frame state and a timing flaw in the restore experiment.

Read this together with `docs/MUGEN_RESUME_INVESTIGATION.md`.

## What remains proven from run #54

A visible, Normal HTC Home Window can survive a reproduced bad early wake when only its existing WPF HwndTarget is disabled before suspend. No process restart, HWND recreation, Window.Hide, or Minimize is required for protection.

On that bad wake Baseline poisoned in the familiar DUCE/MediaContext path while the HwndTarget Disable control remained healthy and was later restored with the same HWND.

The primary poison still occurs during early wake/SxTransition before the ordinary .NET `PowerModes.Resume` callback reaches the application.

## Run #55: Target12 produced a different failure class

The timing matrix was intended to compare restore at Resume +0 / +3 / +12 seconds. On the observed wake:

```text
TV / Baseline      -> working
left / target0     -> working
right / target3    -> working
main / target12    -> old frame remained frozen at 18:12
Manager from tray  -> Win32 frame/menu responsive, WPF client appeared white
```

The main/target12 process was **not MediaSystem-poisoned**. Its dedicated fresh STA Dispatcher successfully created a new HwndSource/MediaContext and rendered the probe. The existing application Dispatcher also remained responsive.

The user manually demonstrated that the stale main widget could:

- be dragged around the desktop;
- open and operate its context menu;
- accept the `Refresh` command;
- accept a change to the `Show 5-day forecast` setting;

while the displayed frame still showed 18:12 and the old forecast strip. This proves the stale image is not an ordinary UI-thread hang. DWM can move the existing surface and the app can process input/state changes while the original HwndTarget fails to present a new frame.

## Timing flaw discovered in run #55

The +0 and +3 modes used a background sleep followed by `Dispatcher.BeginInvoke`. During resume the UI Dispatcher was busy for roughly 9–10 seconds, so the actual target enable times did not match the labels:

```text
target0 restore request: immediately at Resume
actual UI-thread restore: about +10s

target3 restore request: +3s
actual UI-thread restore: about +9–10s
```

Therefore run #55 cannot establish a minimum safe time delay. Future timing comparisons must execute synchronously on the actual event boundary instead of sleeping and then queueing work.

## Diagnostic correction: `_renderOp` was the wrong field

The previous passive MediaContext probe logged `_renderOp`. .NET Framework WPF uses `_currentRenderOp` for the queued Dispatcher render operation. A `<null>` value from the old probe therefore did not prove that no render operation was queued; the field simply did not exist and the old logger collapsed missing fields to null.

The corrected probe records:

```text
_currentRenderOp + DispatcherOperation Status/Priority
_inputMarkerOp
_isRendering
_isDisposed
_isConnected
_isDisconnecting
_promoteRenderOpToInput
_promoteRenderOpToRender
_estimatedNextVSyncTimer
```

alongside the existing HwndTarget and interlock/commit state.

## Relevant WPF source behavior

The .NET Framework WPF HwndTarget implementation does several things that directly matter to this investigation:

1. `UpdateWindowSettings(true)` already calls `MediaContext.PostRender()`. Therefore an extra `InvalidateVisual()` is not a meaningful standalone fix if normal re-enable has already failed to present.
2. `UpdateWindowSettings(false)` posts WPF's private `s_updateWindowSettings` message to avoid accidentally leaving a visible target disabled. The experiment suppresses that auto-reenable while the machine crosses the vulnerable early-wake interval.
3. On `WM_POWERBROADCAST` resume, HwndTarget clears `_isSuspended`, optionally invalidates if `_needsRePresentOnWake`, calls `DoPaint()`, and updates `_lastWakeOrUnlockEvent`.
4. `WM_PAINT` -> `DoPaint()` converts the native dirty region into a WPF composition-target invalidate. This is a stronger re-present path than a visual-tree invalidation alone.

The run #55 stale-frame case is therefore consistent with holding the target disabled while WPF processed its normal power-resume paint/re-present path, then enabling it later after that opportunity had already passed.

## Next matrix: event boundary + re-present path

The existing saved profile slots are reused without manual reconfiguration:

```text
TV / normal       -> Baseline
left / target0    -> TargetOff, synchronously re-enable inside PowerModes.Resume
right / target3   -> TargetOff, re-enable on first DisplaySettingsChanged; no extra paint
main / target12   -> TargetOff, re-enable on the same DisplaySettingsChanged + Win32 InvalidateRect/UpdateWindow
```

The right/main comparison is the key A/B test. Both restore from the same type of system event; the only intended difference is whether the restored target is forced through a real native WM_PAINT/DoPaint re-present.

Interpretation:

- right stale, main working -> the missing native re-present/dirty-region step is strongly implicated;
- both working -> the previous stale frame was timing/order dependent and we need the corrected `_currentRenderOp` trace;
- both stale -> WM_PAINT alone is insufficient and the next target is the MediaContext render operation / composition channel state;
- left survives a natural bad wake -> synchronous PowerModes.Resume is a strong candidate for the eventual almost-invisible product fix.

A fallback restores DisplayChanged modes after 18 seconds if Windows emits no display-change event. The fresh same-PID health probe runs at +23 seconds.

## Manager white-client investigation

Manager has now shown a white WPF client while its native frame/tray/context actions remain responsive. The existing Manager fresh-WPF probe has repeatedly remained healthy, so the next build passively logs the Manager main Window's HwndTarget and MediaContext on visibility/state transitions, including opening from tray.

This should tell us whether the Manager white-client symptom belongs to the same stale-presentation class as the run #55 main widget.

## Reliable poisoned-state marker

Tier 0 remains only an observation. The reliable poison marker is still inability to create a fresh MediaContext/HwndSource in the same PID with the `MediaSystem.ConnectChannels -> DUCE.Channel.SyncFlush` OOM path.
