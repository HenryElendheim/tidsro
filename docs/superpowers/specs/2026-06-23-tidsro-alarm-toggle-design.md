# Tidsro — per-alarm on/off toggle (design)

**Date:** 2026-06-23
**Status:** Approved — ready for plan
**Origin:** Feedback from Henry. Summer vacation is approaching; recurring weekday alarms
should be switchable off for the break and back on in the fall, instead of deleting the
whole set and re-creating it later.

## Goal

Add a toggle to each scheduled alarm so it can be turned off without deleting it. A
disabled alarm stays in the Schedule, keeps all its settings, never fires or warns, and
can be switched back on at any time.

## Scope

**In scope**
- A toggle on **every** alarm row — one-shot (clock-time) and recurring (weekday) alike.
- Disabled alarms persist across restarts, stay visible in the Schedule, and are parked
  in a muted group at the bottom of the list.
- Re-enabling a recurring alarm resumes it from its next future occurrence.

**Out of scope** (not now)
- A "turn all off / on" bulk control.
- Auto-scheduling the toggle by date (e.g. "off until August 15").
- Any change to Quick timers — this is Schedule-only.

## Design

### 1. Data model

- `TimerItem` (runtime, backs both alarm kinds): add `bool IsEnabled { get; set; } = true;`.
- `AlarmRecord` and `RecurringAlarmRecord` (persisted): add `bool Enabled { get; set; } = true;`.
- Bump `TidsroData.CurrentSchema` 3 → 4. No migration code is required: System.Text.Json
  leaves a property absent from the JSON at its initializer default, so every alarm in an
  existing `data.json` (which has no `Enabled` key) loads as `true` — on.

### 2. Scheduler (`SchedulerService`)

- `ArmClockAlarm` and `ArmRecurringAlarm` take a new `bool enabled = true` argument and set
  `IsEnabled` on the created `TimerItem`.
- `Tick()` skip guard gains one condition:
  `if (alarm.State != TimerState.Running || !alarm.IsEnabled || alarm.EndsAt is not { } end) continue;`
  This suppresses **both** the fire and the 5-minute pre-alarm warning for a disabled alarm.
- New `void SetEnabled(TimerItem alarm, bool enabled)`:
  - Sets `alarm.IsEnabled = enabled`.
  - **Re-enabling a recurring alarm rolls it forward, not into an instant fire.** When
    enabling a *recurring* alarm whose `EndsAt` is no longer in the future, set
    `EndsAt = RecurrenceRules.NextOccurrence(Now, end.Hour, end.Minute, days)` and re-arm the
    warning guard (`WarningSent = WarnBefore && Now >= EndsAt - WarningLead`). This avoids
    both an instant fire and a stale "missed while away" note for an occurrence that passed
    while the alarm was intentionally off.
  - A one-shot needs no special handling: if its time is still in the future it fires
    normally once re-enabled; if its time already passed, the next tick resolves it exactly
    as it does any past alarm (fires only inside the 5-minute grace, otherwise a quiet
    missed-note + removal). In practice a stale one-shot is deleted rather than re-enabled.

### 3. List behavior (`MainViewModel`)

- New `ToggleAlarmCommand(AlarmItemViewModel row)`:
  1. `CommitPendingDelete()` (settle any outstanding undo first, like the other actions).
  2. `_scheduler.SetEnabled(row.Item, !row.Item.IsEnabled)`.
  3. `RebuildAgenda()`.
  4. `AlarmsChanged` (persist immediately).
  5. Announce "Alarm at HH:mm turned off" / "turned on".
  - No undo entry — the toggle is its own undo.
- `RebuildAgenda()` ordering: **enabled alarms first** in fire-time order, then **disabled
  parked below** sorted by time-of-day (`EndsAt?.TimeOfDay`, so a stale date never sorts a
  disabled alarm to the top). `isNext` is true only for index 0 **and only when that alarm
  is enabled** — so when every alarm is off, nothing is marked "next".

### 4. UI (`MainWindow.xaml`)

- Reuse the existing gold `ToggleSwitch` style (a restyled `CheckBox`) on each agenda row,
  compact (no text content), grouped with the row's Edit/Delete actions.
  `IsChecked="{Binding IsEnabled, Mode=OneWay}"` + `Command="{Binding DataContext.ToggleAlarmCommand …}"`
  `CommandParameter="{Binding}"`. The command does the real work and `RebuildAgenda` re-creates
  the row, so the switch reflects the model.
- A disabled row reads as off: dimmed (reduced opacity via an `IsEnabled = False` data trigger)
  and, because `IsNext` is false, no gold "next" dot or accent border.

### 5. Accessibility

- The switch identifies its alarm by time (e.g. name "Alarm at 07:00"); its checked/unchecked
  state announces on/off natively to screen readers.
- `AlarmItemViewModel.AccessibleName` gains ", off" when the alarm is disabled.
- Toggling announces "Alarm at HH:mm turned off / on" through the existing UIA announcement
  channel (no focus change).
- Off-state is carried by text + thumb position + dimming together — never colour alone (§7).

### 6. Persistence & back-compat

- `App.ToRecord` / `ToRecurringRecord`: add `Enabled = a.IsEnabled`.
- `App.ArmLoadedAlarms` / `ArmLoadedRecurring`: pass `r.Enabled` into the arm calls.
- `TidsroData.Sanitized()`: copy `Enabled` through for both record types (default `true`,
  so a missing key stays on).

## Edge cases

- **Re-enable a recurring alarm after the break** → rolls forward to the next future
  occurrence; no instant fire, no missed-note.
- **All alarms off** → list shows the muted group only; nothing is "next".
- **Toggle off the current "next" alarm** → the gold highlight moves to the next enabled
  alarm; if none remain, no highlight.
- **Re-enable a one-shot whose time already passed** → resolves on the next tick like any
  past alarm (quiet missed-note + removal beyond grace). Accepted as a rare case.

## Affected files

- `Models/TimerItem.cs`, `Models/AlarmRecord.cs`, `Models/RecurringAlarmRecord.cs`,
  `Models/TidsroData.cs`
- `Services/SchedulerService.cs`
- `ViewModels/MainViewModel.cs`, `ViewModels/AlarmItemViewModel.cs`
- `Views/MainWindow.xaml`
- `App.xaml.cs` (record mapping + loading)
- Tests in `tests/Tidsro.Tests/` (xUnit)

## Testing (TDD, red → green)

- **Scheduler:** a disabled alarm past its time does not fire; a disabled alarm does not
  raise the warning; re-enabling a recurring alarm with a past `EndsAt` rolls it forward to a
  future occurrence; new alarms default to enabled.
- **MainViewModel:** toggling raises `AlarmsChanged`; a disabled alarm parks below the enabled
  ones; toggling off the soonest alarm moves "next" to the next enabled alarm; with all alarms
  off, none is "next".
- **Persistence:** `Enabled` round-trips through save/load; a record with no `Enabled` key
  loads as `true`; `Sanitized()` preserves `Enabled`.
- **AlarmItemViewModel:** `AccessibleName` includes the off state when disabled.

## Manual acceptance (gates release)

- Add a few alarms, toggle some off → they dim and drop to the bottom; "next" highlight is
  correct.
- Restart the app → disabled alarms are still present and still off.
- Re-enable a recurring alarm → it returns to the active group at its next future time and
  fires there.
- Screen-reader pass: switch state and the on/off announcement read correctly.
