# HTC Home Mugen — latest resume investigation result

Updated: 2026-09-02 for run #58 candidate product behavior.

Read this together with `docs/MUGEN_RESUME_INVESTIGATION.md`.

## Root cause model

The hibernate/resume failure is a process-local WPF/DUCE low-power transition race. A visible WPF Window whose existing `HwndTarget` remains render-target enabled can continue MediaContext/channel work while the graphics stack enters or leaves low-power state. On an unlucky transition a DUCE `SyncFlush` stops making forward progress; the process-local WPF MediaSystem then becomes poisoned.

The reliable poison marker is not RenderCapability Tier and not a frozen clock. It is failure to create a fresh same-PID MediaContext/HwndSource, with the characteristic path:

```text
MediaSystem.ConnectChannels
-> MediaContext.CreateChannels
-> DUCE.Channel.SyncFlush
-> OutOfMemoryException
```

Once this state exists, a new Window, HWND, HwndTarget, Dispatcher, or MediaContext inside the same PID does not recover it.

## Proven prevention boundary

The narrow prevention mechanism is:

```text
PowerModes.Suspend
  -> existing HwndTarget.UpdateWindowSettings(false)
  -> suppress WPF's private visible-window auto-reenable message

PowerModes.Resume
  -> remove suppression hook
  -> synchronously call UpdateWindowSettings(true) on the same HwndTarget
```

The Window stays visible and Normal. The process, Window, HWND, position and z-order are preserved. `Hide`, `Minimize`, DWM Cloak and process restart are not required.

A one-shot watchdog observes the old MediaContext after restore. Healthy targets are untouched. If an enabled same-HWND target fails to produce a new commit, the watchdog sends one `InvalidateRect + UpdateWindow` native repaint kick to cover the separate healthy-process stale-surface edge case discovered in run #55.

## Run #57 validation

Run #57 used one unprotected TV/Baseline profile and three identical protected profiles.

Two consecutive natural bad transitions produced the same A/B result:

```text
bad cycle #1: TV/Baseline poisoned; left/right/main protected profiles healthy
bad cycle #2: TV/Baseline poisoned; left/right/main protected profiles healthy
```

Across those two bad cycles the protected side produced six out of six healthy observations. Their watchdogs saw normal render/commit progress and did not need the emergency WM_PAINT path. Their independent fresh Dispatcher/HwndSource probes remained healthy at Tier 2 while the Baseline TV failed the fresh same-PID probe with the characteristic ConnectChannels/SyncFlush OOM.

This is sufficient to promote the guard from a laboratory profile option to the normal profile launch path for the next candidate build.

## Run #58 behavior

HTC Home Mugen Manager now launches **every profile** with the validated guard:

```text
HTCHome.exe --profile <id> --resume-diag target0
```

The old per-profile Baseline / Prototype-fix UI is hidden. Existing profile config fields remain readable for compatibility, but Manager no longer uses a saved Baseline choice when starting a process.

An unprotected control remains available only for deliberate manual diagnostics:

```text
HTCHome.exe --profile <id> --resume-diag normal
```

This prevents an old saved `normal` value (notably the former TV control profile) from accidentally leaving a normal user profile unprotected.

## NVIDIA status

The FrameView/NVIDIA DLL hypothesis is no longer considered the root cause. NVIDIA injection may affect timing but the decisive A/B split follows HwndTarget protection state under otherwise shared graphics conditions.

Run #58 should therefore be exercised in the user's ordinary NVIDIA environment, including the normal NVIDIA overlay. The NVIDIA diagnostics page may remain useful for observing DLL presence and handle trends, but FrameView exclusions are not required for the resume guard.

Small handle changes of a few handles between samples are expected; the historical diagnostic concern was sustained unbounded growth correlated with resume cycles, not ordinary +1/+2 fluctuations.

## Validation goal for #58

Use normal daily hibernate/resume behavior with all four profiles protected and NVIDIA returned to the user's normal configuration. Watch for:

- any protected profile poisoning or freezing;
- any watchdog WM_PAINT recovery events;
- any Manager white-client recurrence;
- sustained abnormal NVIDIA handle growth rather than small fluctuations.

If several days / roughly 15–25 ordinary sleep-resume cycles complete without a protected failure, the guard can move from candidate behavior toward the default release implementation and the heavy diagnostic probes can be reduced.
