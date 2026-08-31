# HTC Home Mugen — latest resume investigation result

Updated: 2026-08-31 after run #53 HwndTarget/MediaContext state logs from a repeated bad hibernate cycle.

Read this together with `docs/MUGEN_RESUME_INVESTIGATION.md`.

## Repeated matrix result

The four-way matrix reproduced the same split on two bad cycles:

```text
Baseline / TV       -> poisoned / bad
WPF Hide / left     -> healthy
DWM Cloak / right   -> poisoned / bad
Minimize / main     -> healthy
```

This makes the result substantially stronger than a one-off profile correlation. DWM Cloak successfully removed the HWND from DWM presentation while WPF still considered the Window visible, normal and non-minimized, yet it failed with Baseline. Hide and Minimize survived.

The user also reports a practical UX distinction: after restore, the left/WPF-Hide widget does not noticeably jump above ordinary windows, while the main/Minimize widget can reappear above them. This is only a UX observation, not a causal result, but it makes Hide a better reference behavior if a later product-safe presentation strategy is needed.

## New key finding: poisoning starts during Suspend

Run #53 added passive private-state snapshots and exposed an earlier failure point than the post-resume display reconfiguration we had been focusing on.

On a bad cycle, the first real WPF OutOfMemoryException in the Baseline and Cloak processes occurred at the transition into hibernate, around the Suspend event, before the later resume-time DisplaySettingsChanged burst. The stack involved the interlocked-presentation/vsync path:

```text
System.OutOfMemoryException
  at System.Windows.Media.MediaContext.CompleteRender()
  at System.Windows.Media.MediaContext.LeaveInterlockedPresentation()
  at System.Windows.Media.MediaContext.ScheduleNextRenderOp(...)
  at System.Windows.Media.MediaContext.EstimatedNextVSyncTimeExpired(...)
  at System.Windows.Threading.DispatcherTimer.FireTick(...)
```

The state split immediately after Suspend intervention was approximately:

```text
Baseline: _isSuspended=True, _isRenderTargetEnabled=True,  _isMinimized=False
Cloak:    _isSuspended=True, _isRenderTargetEnabled=True,  _isMinimized=False
Hide:     _isSuspended=True, _isRenderTargetEnabled=False, _isMinimized=False
Minimize: _isSuspended=True, _isRenderTargetEnabled=True,  _isMinimized=True
```

The disable cookie also advanced on the protected paths as WPF changed presentation state.

This shifts the leading hypothesis. The vulnerable event is now likely an active WPF presentation/render path that remains eligible for an estimated-next-vsync/render operation while the MediaContext/HwndTarget is entering suspended graphics state. The late display-topology burst after resume may still aggravate a damaged process, but it is no longer required as the primary trigger on this reproduced bad cycle.

## Diagnostic contamination found in run #53

The new HwndTarget timeline itself exposed a probe bug: it asked for `RenderCapability.Tier` from a background timeline thread. In a process that was already poisoned, this could create a fresh thread-local MediaContext, hit the familiar `DUCE.Channel.SyncFlush -> MediaContext.CreateChannels -> MediaSystem.ConnectChannels` OOM, and become an unhandled background exception that terminated the PID.

Therefore the fact that Baseline/Cloak appeared as `Stopped` in Manager in run #53 is partly diagnostic contamination. The poisoning itself and the earlier Suspend-time OOM are real; the final process termination was accelerated by the state probe.

The next build removes all background-thread Tier queries from the passive state timeline. The dedicated fresh-Dispatcher probe remains because it catches its own OOM and is intentionally the health test.

## Next matrix: isolate HwndTarget render-target enable state

DWM Cloak has completed its purpose and is replaced with `HwndTarget Disable`:

```text
TV               -> Baseline
Монитор слева    -> WPF Hide
Монитор справа   -> HwndTarget Disable
Основной монитор -> Minimize
```

`HwndTarget Disable` is deliberately narrower than Hide:

- Window.IsVisible remains true;
- WindowState remains Normal;
- HWND remains the same;
- WPF's existing private `HwndTarget.UpdateWindowSettings(false)` path is invoked;
- WPF's internal auto-reenable message is suppressed during the protected interval;
- on restore the suppression hook is removed and the same HwndTarget is enabled again.

The Manager maps the old stored `cloak` assignment to `targetoff` automatically, so the existing right-monitor profile becomes the new control without manual profile editing.

If Baseline fails while TargetOff + Hide + Minimize survive the same bad Suspend, that will be strong evidence that disabling normal HwndTarget presentation/render participation is sufficient protection even when the HWND remains visible and normal.

## MediaContext state timeline

The passive probe now also caches the already-existing UI-thread MediaContext and records selected private state without creating a new MediaContext on the timeline thread:

```text
_interlockState
_needToCommitChannel
_commitPendingAfterRender
_animationRenderRate
_lastPresentationResults
_lastCommitTime
_renderOp
_estimatedNextVSyncTimer
```

The goal is to correlate the successful/failed matrix paths with the exact interlocked-presentation/vsync state around Suspend and the first OOM.

## Reliable bad-state marker

`Tier 0` remains only an observation, not the definition of the bug. Healthy protected controls can initially resume at Tier 0 and later create a healthy Tier-2 MediaContext. The reliable poisoned-state marker remains inability to create a fresh MediaContext/HwndSource in the same PID with the DUCE OOM stack.

## Earlier decisive results retained

- Four visible long-lived profile processes can all poison on the same bad wake; fresh Dispatcher reconstruction in the same PID then fails.
- A single WPF-Hide control survived a bad wake while three visible peers failed, with the same HWND before and after hide/show.
- The hidden Mugen Manager can remain healthy on a bad HTC Home wake, proving the failure is not necessarily machine-wide across every WPF process.
- DWM Cloak is not sufficient protection.
- literal RAM exhaustion, right-click, weather Storyboards, AllowsTransparency, render mode, and same-PID Dispatcher/HwndTarget reconstruction are not necessary causes or viable recovery boundaries.

Build-trigger note: run #54 validates the TargetOff/MediaContext instrumentation added after this result.
