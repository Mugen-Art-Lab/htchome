# HTC Home Mugen — latest resume investigation result

Updated: 2026-09-01 after run #56 reproduced a true Baseline poison while every protected HwndTarget path survived.

Read this together with `docs/MUGEN_RESUME_INVESTIGATION.md`.

## Run #56: true bad transition, Baseline only

Matrix:

```text
TV / normal       -> Baseline
left / target0    -> TargetOff, synchronous re-enable at PowerModes.Resume
right / target3   -> TargetOff, re-enable at DisplaySettingsChanged
main / target12   -> TargetOff, DisplaySettingsChanged + native WM_PAINT kick
```

On the reproduced transition the user observed one HTC Home OOM dialog and the TV/Baseline widget failed, while all three protected widgets continued working. Mugen Manager also returned normally from tray.

The TV/Baseline OOM was logged before the user saw the dialog. On this cycle it occurred immediately after the system's Suspend transition while the WPF HwndTarget was already marked suspended but remained render-target enabled. The process subsequently exhibited the familiar poisoned state: MediaContext channel/interlock activity stopped making progress and the dedicated fresh STA Dispatcher could not create a new MediaContext/HwndSource in the same PID, failing in `MediaSystem.ConnectChannels -> DUCE.Channel.SyncFlush`.

This matters because earlier bad cycles placed the first OOM during early wake/SxTransition. Taken together, the evidence no longer supports one exact callback such as DisplaySettingsChanged or one exact side of the sleep transition as the sole trigger. The common condition is an **active WPF HwndTarget/MediaContext participating in DUCE rendering/channel synchronization while the graphics stack crosses a low-power transition**.

All three protected profiles had the same essential precondition removed: their existing HwndTarget was disabled before the machine crossed the vulnerable transition. They kept the same Window, same HWND, and same process.

## Run #56 re-present comparison

The run also clarified the second, non-poisoned stale-frame effect found in run #55.

- synchronous `UpdateWindowSettings(true)` at PowerModes.Resume survived and returned to normal presentation;
- restore on DisplaySettingsChanged without a native repaint survived;
- restore on the same event plus `InvalidateRect/UpdateWindow` also survived.

Therefore WM_PAINT is **not required on the normal successful restore path**. The run #55 stale 18:12 frame is best treated as a missed/late re-present edge case caused by the previous `Sleep + Dispatcher.BeginInvoke` ordering rather than proof that every HwndTarget restore needs a forced repaint.

The corrected MediaContext trace uses the real `_currentRenderOp` field rather than the earlier incorrect `_renderOp` name.

## Manager result

Manager remained healthy on run #56. Its new window-state probe recorded the normal tray-return sequence:

```text
Window becomes visible while HwndTarget is still disabled
-> HwndTarget becomes enabled
-> MediaContext has a Pending Render DispatcherOperation
-> within a few hundred milliseconds the render operation is gone/completed
-> client area is rendered normally
```

This provides a useful healthy reference for a future white-client Manager occurrence.

## Strongest current root-cause model

The original HTC Home hibernate bug is now best modeled as a WPF/DUCE low-power transition race:

```text
visible/Normal WPF Window
+ active HwndTarget render target
+ MediaContext render/channel work
+ graphics stack entering or leaving low-power transition
-> unlucky DUCE SyncFlush / presentation synchronization failure
-> MediaContext stops making forward progress
-> process-local WPF MediaSystem becomes poisoned
```

Once poisoned, creating a new Window, HwndTarget, Dispatcher, or MediaContext inside the same PID does not recover the process. The reliable bad-state marker remains a fresh same-PID MediaContext/HwndSource failing in `MediaSystem.ConnectChannels -> DUCE.Channel.SyncFlush`.

The successful prevention boundary is much earlier and narrower: disable the **existing HwndTarget render target before Suspend**, then re-enable that same target after ordinary PowerModes.Resume reaches the application.

No Window.Hide, WindowState.Minimized, HWND recreation, process restart, or DWM Cloak is required.

## Next build: prototype fix

The next build converges all previously protected laboratory modes onto one candidate product behavior:

```text
TV               -> Baseline / no protection
left             -> Prototype fix
right            -> Prototype fix
main             -> Prototype fix
```

Prototype fix:

```text
PowerModes.Suspend
  -> invoke WPF's existing HwndTarget.UpdateWindowSettings(false)
  -> suppress WPF's private visible-window auto-reenable message during sleep transition

PowerModes.Resume
  -> remove suppression hook
  -> synchronously invoke UpdateWindowSettings(true) on the same HwndTarget
```

All Window/ HWND identity and z-order state remain unchanged.

### Stale-surface watchdog

Run #55 proved that a healthy process can occasionally have a re-enabled HwndTarget that keeps presenting an old DWM frame if restoration happens in an unfortunate order. The prototype therefore adds a one-shot passive watchdog rather than forcing WM_PAINT on every resume.

After the normal synchronous restore it observes the original MediaContext's `_lastCommitTime` and `_currentRenderOp`. If no new commit is observed after a short grace period while the target is enabled and the HWND is unchanged, it sends one native `InvalidateRect + UpdateWindow` kick and logs the result. Healthy targets are left untouched.

This keeps the normal path as close as possible to stock WPF while providing an emergency re-present for the separate stale-surface failure class.

## Validation goal

Repeated bad cycles should now produce the strongest possible A/B test:

```text
Baseline poisons while all three Prototype-fix profiles survive
```

If that result repeats across several naturally occurring bad transitions, the HwndTarget Suspend/Resume guard is ready to move from diagnostic mode toward the default profile behavior. The Manager white-client symptom should remain separately instrumented until its own state trace is captured on failure.
