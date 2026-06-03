# Tidsro — Design Specification

- **Status:** Draft for review
- **Date:** 2026-06-03
- **Author:** Malin Fossum
- **Type:** Desktop application design spec

---

## 1. Overview

Tidsro is a tiny, calm, dark-mode-first **Windows desktop timer and alarm app**, built in **C# / WPF**. It runs quietly in the system tray and helps structure a focused workday: ad-hoc countdowns (pomodoro, "code hour", breaks), clock-time alarms ("lunch at 12:00"), and recurring alarms (every weekday at 09:30). Each timer carries an optional short label and either stays silent (the default) or plays a single gentle sound. When a timer finishes, Tidsro shows a quiet card in the bottom-right corner that **never steals focus** and **stays until dismissed**.

The name is Norwegian: *tid* (time) + *ro* (calm / peace) — "calm time". It reflects the tool's whole intent: visible but never flashy, present but never intrusive.

> **Why it exists:** Most timers are either too bare to plan a day around or too noisy to keep open while you focus. Tidsro stays simple and local, but adds a cleaner, more accessible, modern design, multiple simultaneous timers, recurring alarms, and a calm completion experience — for people who work with notifications off and don't want to reach for their phone.

---

## 2. Goals & non-goals

**Goals**
- A daily go-to focus tool — polished enough to reach for every day, and genuinely useful to others too.
- Local-first, low-RAM, runs in the background in the tray.
- Calm, accessible, dark-mode-first UI.
- Countdowns + clock-time + recurring alarms, each labelled, silent-or-gentle-sound.
- A completion alert that is impossible to miss yet never annoying.

**Non-goals (for now)**
- Mobile (a someday hope, possibly via the separate **Ignite** task app — explicitly out of scope here).
- Cloud sync / backup (wanted, but a planned future slice — see §6).
- Custom user-supplied sound files (later slice).
- Light theme; themes/skins.
- An auto-cycling pomodoro engine (the popup's **Restart** action covers looping).

---

## 3. Users & key use cases

Primary user: a developer working heads-down with Windows notifications off.

- "Give me 25 focused minutes, then nudge me" — countdown, silent.
- "Set up my day" — 09:30 standup, 12:00 lunch, 14:00 meeting prep, 16:30 stretch; some recurring.
- "5 minutes before my meeting" — countdown or clock-time.
- "Pomodoro loop" — start 25 min; on finish hit **Restart**.
- "Tea's ready" — short countdown with a gentle chime.

---

## 4. Architecture

**Stack:** C# / WPF on .NET (version per the `csharp-wpf` scaffold). Pattern: **MVVM** — WPF's native expression of the MVC separation already used on the web.

| Layer | In Tidsro | Rule |
|---|---|---|
| **Model** | Domain entities + services: timer/alarm items, the scheduler (ticking brain), persistence, sound, startup, recurrence math. No UI. | State & logic only; unit-testable. |
| **View** | XAML: main window, completion popup, settings, tray icon. | No logic; binds to ViewModels. |
| **ViewModel** | Exposes state and commands (start, pause, dismiss, +5 min, restart, edit) to the View; subscribes to the scheduler. | Glue only; no XAML, no file/registry access (delegates to services). |

**Proposed project structure** (refine during planning):

```
Tidsro/
  Tidsro.sln
  src/Tidsro/
    App.xaml(.cs)              // startup -> tray; no window shown by default
    Models/
      TimerItem.cs             // one timer/alarm (see §7)
      TriggerType.cs           // Countdown | ClockTime | Recurring
      Recurrence.cs            // days-of-week + time-of-day
      SoundChoice.cs           // None (silent) | one of the built-ins
    Services/
      SchedulerService.cs      // single ~1s tick; raises Fired(item)
      PersistenceService.cs    // System.Text.Json <-> %AppData%\Tidsro\data.json
      SoundService.cs          // play a gentle built-in sound
      StartupService.cs        // launch-at-startup toggle (registry Run key)
      TrayService.cs           // tray icon + menu (Open, Quit)
    ViewModels/
      MainViewModel.cs, TimerItemViewModel.cs,
      AlarmEditViewModel.cs, SettingsViewModel.cs, PopupViewModel.cs
    Views/
      MainWindow.xaml, CompletionPopup.xaml, SettingsWindow.xaml
    Assets/   sounds/ (3-5 .wav), icons/
    Resources/ tokens.xaml     // mirrors design-system tokens (palette, spacing, type)
  tests/Tidsro.Tests/          // scheduler, recurrence, persistence
```

**Dependencies — keep minimal (confirm in planning):**
- **Tray icon:** WPF has no native tray. Lean **H.NotifyIcon.Wpf** (clean, popular) — or built-in `System.Windows.Forms.NotifyIcon` for zero extra packages.
- **Sound:** built-in `System.Media.SoundPlayer` (WAV, simplest) or WPF `MediaPlayer`. No NuGet.
- **JSON:** built-in `System.Text.Json`.
- No other dependencies planned.

---

## 5. Feature spec

### 5.1 Triggers
- **Countdown** — a duration (HH:MM:SS); fires at zero. Presets 15 / 30 / 60 min + custom.
- **Clock-time alarm** — fires at a specific time today (e.g., 14:00). One-shot.
- **Recurring alarm** — fires at a time on selected days (daily, or pick weekdays). Reschedules to the next occurrence after firing.

### 5.2 Main window (Layout B — two zones)
- **Quick timers** — presets `15 / 30 / 60` + "custom", an optional label field, and the list of running countdowns (label, remaining time, silent/sound tag, pause/cancel).
- **Your day** — agenda of scheduled + recurring alarms sorted by next fire time (time, label, daily/once tag, edit/delete). Friendly empty-state until Slice 2 ("Nothing scheduled yet — add an alarm").
- **Window chrome:** the close button (✕) **minimizes to tray** (keeps running). Real quit lives in the tray menu. The app starts to the tray; opening the tray icon shows this window.

### 5.3 Completion alert
- A small card in the **bottom-right** of the working area.
- **Topmost but non-activating** (`Topmost=true`, `ShowActivated=false`, `ShowInTaskbar=false`): it appears over other windows but **does not steal keyboard focus** — the user keeps typing.
- **Persists until dismissed.** No auto-fade, no flashing.
- **At rest:** label + "complete" + ✕. **On hover:** three quiet actions — **+5 min** (extend / snooze), **Restart** (re-run the original duration; pomodoro loop), **Dismiss**.
- **Stacking:** multiple finished timers stack upward from the corner; each dismissed independently.
- **Silent timers:** visual only, zero sound. **Sound timers:** play the chosen gentle built-in once (or a couple of soft repeats) then stop; the card remains regardless.
- Styling from `tokens.xaml` (mirrors `design-system`); subtle fade-in, respecting reduced-motion (see §9).

### 5.4 Sounds
- 3–5 curated, gentle, non-jarring built-in sounds (e.g., soft chime, marimba, gentle bell), bundled as assets.
- Chosen per-timer; **silent is the default.** Played via a built-in API; plays once/short, never a nagging loop. No custom files yet (later slice).

### 5.5 Tray & background
- Single lightweight process living in the tray. Tray menu: **Open**, **Quit**. Left-click tray → open main window.
- Honest footprint: WPF idles at a few tens of MB — heavier than bare Win32, far lighter than Electron. Acceptable for "low RAM".

### 5.6 Persistence
- Alarms (clock-time + recurring) and settings saved as JSON to `%AppData%\Tidsro\data.json`, loaded on launch; scheduled/recurring alarms **re-arm to their next future occurrence**.
- Ad-hoc countdowns are **session-only** — a mid-run countdown does not resurrect after restart/reboot.

### 5.7 Settings
- **Launch at startup** toggle — **off by default** (adds/removes a user-scope registry Run entry via `StartupService`).
- **Default sound** for new timers (default: silent).
- Minimal; grows only as needed.

---

## 6. Build slices

1. **Slice 1 — Countdowns (a usable tool).** Countdown engine + presets/custom + labels + main window (Quick timers live, Your day empty-state) + tray + completion popup (with +5 / Restart / Dismiss) + silent/sound + multiple simultaneous + stacking popups. Settings persistence only.
2. **Slice 2 — Clock-time alarms.** One-shot "at HH:MM". The Your day agenda becomes live. Alarm persistence.
3. **Slice 3 — Recurring + full persistence.** Daily / weekday-selectable recurrence, next-occurrence math, re-arm on launch, missed-while-asleep handling (§8).

**Future (post-v1):** cloud sync / backup of alarms & settings (explicitly wanted, deferred); custom sound files; possibly mobile (separate effort).

---

## 7. Data model

`TimerItem` — one record for any trigger:

- `Id` (Guid)
- `Label` (string, optional)
- `TriggerType` (Countdown | ClockTime | Recurring)
- `Duration` (TimeSpan?, for Countdown)
- `TimeOfDay` (TimeOnly?, for ClockTime / Recurring)
- `Recurrence` (set of DaysOfWeek, for Recurring; empty = one-shot)
- `Sound` (SoundChoice; None = silent)
- Runtime-only: `State` (Idle / Running / Paused / Fired), `RemainingOrNextFire` (computed)

Persisted: alarms (ClockTime / Recurring) + settings. Not persisted: running countdown state.

---

## 8. Key behaviours & edge cases

- **Single scheduler tick:** one `DispatcherTimer` (~1s) updates all running countdowns and checks alarm fire times. O(n) over a tiny n; negligible CPU/RAM.
- **App closed / reboot:** countdowns lost; clock-time/recurring re-arm to next future occurrence. **Missed occurrences are skipped, not backlogged** (no flood of old alarms on launch).
- **Sleep / hibernate:** on resume, if a clock-time/recurring alarm's time passed during sleep within a short grace window, fire it (late beats silent for a depended-on alarm); older than the window → skip to next. (Listen for power-resume / detect tick gaps.)
- **DST / time zones:** all times local; recurrence computed against local `DateTime`.
- **Multiple fire at once:** stack popups bottom-right.
- **+5 min on a finished countdown:** re-arms a 5-min countdown from the card. **Restart:** re-runs the original duration.
- **Duplicate labels / durations:** allowed.

---

## 9. Accessibility (first-class)

- Full keyboard operation; visible focus states; logical tab order.
- `AutomationProperties` (names/labels) on interactive controls for screen readers; the completion popup exposes an accessible name.
- Dark-theme contrast meets WCAG AA (tokens chosen for ≥4.5:1 on text).
- Respect **reduced-motion** (OS setting) — skip the fade when set.
- Comfortable hit targets; nothing conveyed by colour alone (tags use text + colour).

---

## 10. Privacy & security

- **Fully local.** No network calls, no accounts — your data stays on your machine, in `%AppData%\Tidsro`. (Cloud sync, when added later, will be opt-in and specified separately.)
- The startup toggle writes a single user-scope registry Run entry; no admin rights required.

---

## 11. Testing

- Unit tests (`dotnet test`) for Model/Services: scheduler firing, recurrence next-occurrence math (weekday selection, DST boundary), persistence round-trip, missed-while-asleep logic.
- ViewModels tested where logic warrants; Views are thin and verified by hand.

---

## 12. README requirement

The English README must explain the name: **Tidsro = Norwegian *tid* (time) + *ro* (calm / peace) → "calm time"**, including the meaning and the intent behind it (calm, focused, unflashy). Plus a one-line description, the stack listed plainly, minimal setup, and no badges or marketing bloat.

---

## 13. Open decisions (resolve in planning / research)

- Tray library: **H.NotifyIcon.Wpf** vs built-in WinForms `NotifyIcon`.
- Sound API: `SoundPlayer` vs `MediaPlayer`; source 3–5 gentle, licence-clear WAVs.
- .NET version & nullable settings from the `csharp-wpf` scaffold.
- Exact bottom-right offset & multi-monitor handling (which screen's working area).
