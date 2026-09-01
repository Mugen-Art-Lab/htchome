# HTC Home Mugen — latest resume investigation result

Updated: 2026-09-01 after run #54 reproduced a bad early-resume cycle with the HwndTarget Disable control.

Read this together with `docs/MUGEN_RESUME_INVESTIGATION.md`.

## Run #54: HwndTarget Disable survives while Baseline poisons

Matrix:

```text
TV               -> Baseline
Монитор слева    -> WPF Hide
Монитор справа   -> HwndTarget Disable
Основной монитор -> Minimize
```

On the reproduced bad cycle the user observed that TV/Baseline froze while the left, right, and main widgets remained functional. Two visible `HTC Home error` message boxes both originated from the TV/Baseline process, not the main-monitor process.

The Baseline process recorded two WPF OutOfMemoryExceptions during the early wake transition. The stacks included:

```text
DUCE.Channel.SyncFlush
MediaContext.NotifyChannelMessage
```

and

```text
MediaContext.CompleteRender
MediaContext.LeaveInterlockedPresentation
MediaContext.ScheduleNextRenderOp
MediaContext.AnimatedRenderMessageHandler
```

Afterwards the existing Baseline MediaContext was observed in `_interlockState=WaitingForResponse`, and the dedicated fresh-Dispatcher probe could no longer create a new MediaContext/HwndSource in that PID, failing in the familiar `MediaSystem.ConnectChannels -> DUCE.Channel.SyncFlush` path. Visually the TV widget remained frozen while the PID stayed alive.

The HwndTarget Disable process survived the exact same bad system wake. Before suspend its Window was still:

```text
IsVisible=True
WindowState=Normal
IsIconic=False
```

The experiment changed only the existing WPF HwndTarget from `_isRenderTargetEnabled=True` to `False`, preserving the Window and HWND. After resume the same HwndTarget was enabled again, the HWND identity remained unchanged, the fresh Dispatcher/MediaContext probe succeeded, and the widget continued working.

This is substantially stronger than the earlier Hide experiment: **neither hiding nor minimizing the Window is required for protection.** Removing the existing HwndTarget from active WPF rendering/presentation participation is sufficient on this reproduced bad wake.

Hide and Minimize also survived, as expected from earlier matrix runs. Hide disables the HwndTarget; Minimize uses WPF's separate minimized rendering path.

## Timing correction: failure is on early resume, before PowerModes.Resume

Earlier notes described the poisoning as starting "during Suspend" because the first OOM appeared between Suspend and the application's Resume callback. Run #54 plus the system black-box timing sharpens that interpretation.

The bad cycle entered suspend around 07:28:49–07:28:50. The Baseline OOMs appeared around 07:30:08 during the system's early wake/SxTransition activity. The ordinary .NET `SystemEvents.PowerModeChanged(PowerModes.Resume)` callback arrived only several seconds later, around 07:30:12.

Therefore the current best timing model is:

```text
Suspend callback
  -> protected modes disable HwndTarget
  -> machine enters low-power/hibernate state
  -> early wake / SxTransition begins
  -> vulnerable active Baseline MediaContext/HwndTarget poisons
  -> only afterwards PowerModes.Resume reaches the application
```

This explains why post-Resume recovery attempts are too late once a PID is poisoned, and why a pre-suspend HwndTarget disable can prevent the failure.

The familiar late `DisplaySettingsChanging/Changed` burst around +10 to +12 seconds after ordinary Resume still occurs, but it is no longer required as the primary trigger for the reproduced failure.

## User-visible behavior

The user prefers the WPF-Hide recovery UX over Minimize because Hide did not noticeably bring the widget above ordinary windows, while restoring from Minimize could alter z-order. HwndTarget Disable is better still in principle: the Window remains visible and Normal throughout, so it should not require a Show or WindowState transition at all.

## Next experiment: minimum safe HwndTarget restore delay

Run #54 kept HwndTarget disabled for 22 seconds after ordinary Resume as a laboratory safety margin. The next matrix removes Hide and Minimize and uses the **same HwndTarget Disable mechanism** with different restore delays:

```text
TV               -> Baseline
Монитор слева    -> HwndTarget Disable, restore immediately at PowerModes.Resume (0s)
Монитор справа   -> HwndTarget Disable, restore at Resume +3s
Основной монитор -> HwndTarget Disable, restore at Resume +12s
```

The old saved profile assignments are transparently mapped into these slots, so no manual reconfiguration is required.

Interpretation on a bad wake:

- Baseline fails; 0s + 3s + 12s all survive -> ordinary PowerModes.Resume is already a safe restore boundary and the eventual fix can be nearly invisible.
- 0s fails; 3s and 12s survive -> a short post-Resume grace period is required.
- 0s and 3s fail; 12s survives -> the vulnerable interval extends well after PowerModes.Resume and likely overlaps display/topology stabilization.
- all protected delays fail -> the previous 22s protection was significant and the timing window is longer than expected.

The dedicated fresh-Dispatcher health probe is moved to +15s so the +12s target has already been restored before the health check.

## Current strongest mechanism hypothesis

A visible, Normal WPF Window can survive the bad hibernate/resume cycle if its existing HwndTarget is disabled before suspend and kept out of normal render/presentation participation through the vulnerable early wake transition. The poison appears to involve MediaContext/DUCE interlocked presentation and channel synchronization while the graphics stack is waking.

A likely product-safe fix, if the timing experiment confirms a short restore boundary, is:

```text
PowerModes.Suspend -> disable each live HwndTarget without hiding/minimizing the Window
PowerModes.Resume  -> re-enable the same HwndTarget after the smallest proven-safe delay
```

No process restart, no HWND recreation, no Window.Hide, and no WindowState change would be required.

## Reliable poisoned-state marker

`Tier 0` is still not the definition of failure. The reliable marker remains inability to create a fresh MediaContext/HwndSource in the same PID with the DUCE OOM stack.
