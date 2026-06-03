# Tidsro

A calm, dark-mode-first desktop timer and alarm for Windows — countdown timers, clock-time alarms, and recurring alarms that nudge you with a quiet corner card instead of a flashy notification.

> **Tidsro** is Norwegian: *tid* (time) + *ro* (calm / peace) — roughly *"calm time."* The name is the whole idea: a timer that's visible when you need it and invisible when you don't.

## Status

Early design. The full design lives in [`docs/superpowers/specs/2026-06-03-tidsro-design.md`](docs/superpowers/specs/2026-06-03-tidsro-design.md). No application code yet — implementation is planned in slices, countdowns first.

## Stack

C# · WPF (.NET) · MVVM. Local-first: no accounts, no network — your data stays on your machine.

## Planned (v1)

- Countdown timers with presets (15 / 30 / 60 min) and custom durations
- Clock-time alarms and recurring (weekday) alarms
- An optional label per timer; silent by default, or a gentle built-in sound
- A quiet bottom-right completion card that never steals focus
- Lives in the system tray, low footprint
