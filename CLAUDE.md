# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Tidsro is a calm, dark-mode-first Windows desktop timer and alarm app: countdown timers plus a Schedule of one-shot and recurring clock-time alarms, surfaced via quiet bottom-right cards that never steal focus. C# / WPF on .NET 10 (`net10.0-windows`), MVVM via CommunityToolkit.Mvvm, tray icon via H.NotifyIcon.Wpf. Local-first: no accounts, no network; data lives in `%AppData%\Tidsro`.

**Windows-only:** the projects target `net10.0-windows` with WPF, so building, testing, and running require Windows.

## Commands

```
dotnet build src/Tidsro/Tidsro.csproj                  # build the app
dotnet run --project src/Tidsro                        # run it
dotnet test tests/Tidsro.Tests/Tidsro.Tests.csproj     # all tests (xUnit)
dotnet test tests/Tidsro.Tests/Tidsro.Tests.csproj --filter "FullyQualifiedName~LogServiceTests.<MethodName>"   # one test
./publish.ps1                                          # dist/: portable Tidsro.exe + Tidsro-Setup.exe (needs Inno Setup)
```

Gotchas:
- A running `Tidsro.exe` locks the build output. Stop it first: `Get-Process Tidsro -ErrorAction SilentlyContinue | Stop-Process -Force`.
- **Zero warnings is the project standard** — match delegate signatures exactly (including `sender` nullability) so no `CS86xx` warnings appear.

## Architecture

MVVM with strict layer rules (see `docs/superpowers/specs/2026-06-03-tidsro-design.md` §4):

- **Models + Services** (`src/Tidsro/Models`, `src/Tidsro/Services`) — all domain state and logic; no UI types; unit-testable.
- **ViewModels** (`src/Tidsro/ViewModels`) — glue only: expose state and commands, subscribe to the scheduler. No XAML, no file or registry access (delegate to services).
- **Views** (`src/Tidsro/Views`) — XAML with no logic; binds to ViewModels. Shared design tokens live in `src/Tidsro/Resources/tokens.xaml`.

Key pieces that span files:

- **`App.xaml.cs` is the composition root.** It installs global exception handlers *first* (a glitch must never silently kill an alarm app — UI-thread exceptions are logged and survived), claims a single-instance mutex (a second launch signals the first to surface its window and exits), wires services to the UI, and runs a 250 ms `DispatcherTimer` heartbeat that calls `SchedulerService.Tick()`, refreshes the ViewModels, and retires fired warning cards.
- **`SchedulerService` is the ticking brain.** It owns the running countdowns and armed alarms, and raises `Fired`, `Warning` (5-min pre-alarm heads-up), and `Expired` events on tick. Two pinned constants: `Grace` (a missed alarm still fires within 5 minutes of its time — this is how firing survives sleep and app relaunch) and `WarningLead` (5 min). Recurrence math lives in `Models/RecurrenceRules.cs`; parsing/validation rules in `CountdownRules.cs` / `ClockTimeRules.cs`.
- **Time is injected, never read directly.** Everything time-dependent takes an `IClock` (`SystemClock` in the app, `FakeClock` in tests). Never call `DateTime.Now` / `DateTimeOffset.Now` in Models/Services. Persisted alarm times are wall-clock local times, tagged `DateTimeKind.Local` before lifting to `DateTimeOffset`.
- **Persistence is snapshot-on-change.** `PersistenceService` serializes `TidsroData` (settings + `AlarmRecord` / `RecurringAlarmRecord` lists — the durable form of runtime `TimerItem`s) to JSON in `%AppData%\Tidsro`. The App saves on every `AlarmsChanged`, on fire, and on quit. Loaded data is untrusted: a bad record is skipped, never allowed to stop launch. A recurring alarm's persisted `NextFireAt` is the dedup marker that stops a quick relaunch from re-firing within grace.
- **Completion cards** (`CompletionPopup` + `PopupViewModel`) are owned and stacked by `App.xaml.cs`, positioned via `ScreenHelper`, shown with `ShowActivated=false` so they never steal focus. Keyboard access goes through the global hotkey (`HotkeyService`, Ctrl+Alt+T) with the tray "Focus latest alert" item as fallback.
- **Sounds are embedded resources** (`Assets/sounds/*.wav` via `<EmbeddedResource>`) so the single-file build carries them; `SoundResourceTests` guards the name↔resource mapping.

Tests (`tests/Tidsro.Tests`) are xUnit, run against internals via `InternalsVisibleTo`, and use `FakeClock` / `FakeSoundService` for determinism. Services are designed testable by construction: paths and clocks come in through constructors (`PersistenceService`, `LogService` follow the same pattern).

## Conventions

- **Formatting** is enforced by `.editorconfig`: file-scoped namespaces, Allman braces, 4-space C#, 2-space XAML/JSON/MD, CRLF, sorted `System` usings first, explicit accessibility modifiers.
- **Commits** use conventional style (`feat:`, `fix:`, `test:`, `docs:`).
- **Privacy rule:** never write user-entered labels to logs (`LogService` records exception details only).
- **Versioning:** the single source of truth is `<Version>` in `src/Tidsro/Tidsro.csproj` (publish.ps1 and the installer read it). Update `CHANGELOG.md` alongside release-worthy changes.
- **Design docs drive features.** `docs/superpowers/specs/` holds design specs and `docs/superpowers/plans/` holds implementation plans; code comments cite spec sections (e.g. "spec §5.3"). Check the relevant spec before changing pinned behavior such as grace windows, focus rules, or persistence semantics.
