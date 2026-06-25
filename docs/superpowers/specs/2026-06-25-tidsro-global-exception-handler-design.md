# Tidsro — global exception handler & crash log (design)

**Date:** 2026-06-25
**Status:** Approved — ready for plan
**Origin:** Backlog item carried since v1.3. The v1.3 completion-card crash was a single
unguarded exception on the 250 ms tick that silently shut the whole app down — an alarm app
vanishing without a trace. This slice adds the permanent net that should have been there:
catch unhandled exceptions app-wide, **keep the UI alive**, and **always leave a record**.

## Goal

An alarm app must never silently disappear. Catch unhandled exceptions across the app, write
every caught error to a log file, keep the app running for UI-thread errors (the common case),
and surface a calm notice so a caught error is **discoverable, never silently swallowed**.

The guiding principle — the v1.3 lesson, encoded: **keep alive *and* make it visible.** Staying
alive protects the app; the log + a calm balloon are what stop this net from *masking* the very
kind of bug it catches. A silent swallow would have hidden the v1.3 crash instead of pointing at
it.

## Scope

**In scope**
- A new `LogService` writing timestamped entries to `%AppData%\Tidsro\tidsro.log`.
- Three runtime hooks: `DispatcherUnhandledException` (UI thread → survive), `AppDomain`
  `UnhandledException` (background → log), `TaskScheduler.UnobservedTaskException` (log).
- The `OnStartup` body guarded, so a startup failure is a logged, explained exit — not a silent
  vanish.
- A calm, non-focus-stealing **tray balloon** on a caught UI-thread error, **throttled** so a
  repeating error logs and notifies once, not in a flood.
- A **"Open log folder"** tray menu item so the log is reachable after a balloon.
- A **size cap**: the log archives to `tidsro.log.old` past ~512 KB, so it can't grow unbounded.

**Out of scope** (not now)
- Telemetry / remote crash reporting / any network call — this is a private, local-only log.
- A crash dialog with "send report", or auto-restart after a fatal crash.
- A general-purpose logging framework (Serilog etc.) or log levels — this is crash logging, not
  app-wide tracing.
- The existing localized `try/catch` blocks (`SaveData`, `ArmLoaded*`, `PersistenceService`) stay
  exactly as they are; this net sits *beneath* them.

## Honest scope of "never vanishes"

This is not a magic "never crashes" wrapper. **UI-thread** exceptions — the tick loop, command
and event handlers, i.e. the vast majority of what runs here — are caught and survived. A truly
fatal **background-thread** crash still exits the process, but now leaves a log entry instead of
disappearing without a trace. That is the accurate promise, and it is a large improvement over
today (zero handlers, zero logging).

## Design

### 1. `LogService` (new — `src/Tidsro/Services/`)

Mirrors `PersistenceService`: a path in the constructor, a `DefaultPath`, all I/O self-contained
and tested against a temp file. Takes `IClock` (like `SchedulerService`) for deterministic
timestamps.

```csharp
public sealed class LogService
{
    public LogService(string path, IClock clock);
    public static string DefaultPath { get; }      // %AppData%\Tidsro\tidsro.log
    public bool Log(Exception ex, string source);   // returns true when surfaced, false when throttled
}
```

- **`Log(ex, source)`**:
  1. Build a signature: `source | exception type | message`.
  2. **Throttle (per signature):** if it matches the last logged signature within
     `DedupeWindow` (5 s), return `false` — suppress the duplicate. Otherwise record the new
     signature + time and continue.
  3. **Size cap:** if the file is larger than `MaxBytes` (512 KB), move it to `tidsro.log.old`
     (single generation, overwrite) and start fresh.
  4. Format the entry and append it.
  5. Return `true` (a fresh, surfaceable error).
- **Never throws.** Steps 3–4 (all file I/O) are wrapped — a logger that throws while logging a
  crash is worse than useless. Note the return value is "was this a *fresh* error worth
  surfacing", decided by the throttle in step 2 — it stays `true` even if the disk write in step 4
  fails, because the user should still be told (the balloon is awareness; the file is the detail).
- **Format** — a pure `static string Format(DateTimeOffset now, Exception ex, string source, Version? version)`
  so it can be asserted directly in tests:
  ```
  ===== 2026-06-25 14:32:01.123 +02:00 · v1.4.0 · DispatcherUnhandledException =====
  System.InvalidOperationException: <message>
     at <full stack, inner exceptions and all>

  ```
  The body is `ex.ToString()` (type + message + stack + inner exceptions, complete). Version from
  `Assembly.GetExecutingAssembly().GetName().Version`, passed into `Format` to keep it pure.

### 2. Wiring in `App.xaml.cs`

`LogService` is built **first** in `OnStartup` and the three handlers installed before any other
work, so even an early failure is caught and recorded.

```csharp
protected override void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);
    InstallExceptionHandlers();          // _log + the three hooks, before anything can throw
    try
    {
        if (!TryClaimSingleInstance()) return;
        LoadStateAndServices();
        WireSchedulerEvents();
        StartTickLoop();
        RegisterHotkey();
        _tray = TrayBuilder.Create(ShowMainWindow, FocusLatestAlert, OpenLogFolder, Quit);
        ShowWindowUnlessBootLaunch(e);
    }
    catch (Exception ex)                  // a startup failure is logged + explained, not silent
    {
        _log.Log(ex, "OnStartup");
        ShowStartupFailure();             // tray balloon if it exists, else a last-resort message
        Shutdown();
    }
}
```

Handlers:

| Hook | Catches | Action |
|---|---|---|
| `DispatcherUnhandledException` | UI-thread errors — tick loop, command/event handlers (**the v1.3 path**) | `Log(...)`; if it returned `true`, show the calm balloon; `e.Handled = true` (**survive**) |
| `AppDomain.CurrentDomain.UnhandledException` | Fatal background-thread errors | `Log(...)` only — runtime is tearing down; best-effort record |
| `TaskScheduler.UnobservedTaskException` | Faulted tasks never awaited | `Log(...)`; `e.SetObserved()` |

`ShowCrashBalloon()` → `_tray?.ShowNotification("Tidsro", "Tidsro hit a problem but is still
running.")` (H.NotifyIcon; exact overload confirmed at implementation). Best-effort via `_tray?`.

### 3. "Open log folder" tray item (`TrayBuilder` + `App`)

- `TrayBuilder.Create` gains an `Action onOpenLog`; a new **"Open log folder"** menu item (grouped
  above Quit) invokes it.
- `App.OpenLogFolder()` ensures the directory exists, then opens Explorer at the log — selecting
  the file if present (`explorer.exe /select,"…\tidsro.log"`), else opening the folder. Wrapped in
  `try/catch` (opening a folder must never crash the app).

### 4. Size cap (inside `LogService`)

One previous generation is kept: past ~512 KB the live log is archived to `tidsro.log.old`
(overwriting any earlier archive) and a fresh `tidsro.log` is started. Bounded forever; the most
recent history is always the live file, the generation before it is one file away.

## Startup-failure fallback

**Decided.** If `OnStartup` fails **before** the tray exists, there's no balloon to show, so the
last-resort is a single one-line `MessageBox` — *"Tidsro couldn't start. See tidsro.log."* — then
exit. It's the one focus-stealing surface in the design, justified because a silent failure to
start is the exact outcome this slice exists to kill, and the app is exiting anyway. Not knowing
is the worst outcome — a visible warning wins over silence here.

## Edge cases

- **Runaway tick** (a bug that throws every 250 ms) → logged + ballooned **once**; identical
  repeats inside 5 s are suppressed; the app keeps ticking. One breadcrumb, no flood — the v1.3
  lesson without re-creating the silence.
- **A different error right after a throttled one** → distinct signature → logged + ballooned
  (the throttle is per-signature, not a global mute).
- **Log file unwritable** (open in an editor, permissions) → the write is swallowed; the app still
  survives, and the balloon still shows (awareness doesn't depend on the disk write).
- **Fatal background-thread crash** → `AppDomain` handler logs; the process still exits (honest
  scope) — but with a record, not a silent vanish.
- **Startup failure** → logged; last-resort message (above); clean `Shutdown()`.

## Affected files

- **New:** `Services/LogService.cs`
- `App.xaml.cs` — install handlers, guard `OnStartup`, balloon, `OpenLogFolder`
- `Services/TrayBuilder.cs` — new `onOpenLog` parameter + menu item
- **New tests:** `tests/Tidsro.Tests/LogServiceTests.cs` (xUnit)

No new fakes needed: `LogService` is tested via a temp file (like `PersistenceServiceTests`) plus
the existing `FakeClock`.

## Testing (TDD, red → green) — `LogServiceTests`

- **Format** contains the timestamp, app version, source tag, exception type, message, and stack.
- **Append:** one `Log` call writes an entry; the file then contains the type and message.
- **Two distinct errors** → two entries.
- **Throttle:** the same signature within 5 s is suppressed (file unchanged, `Log` returns
  `false`); after advancing `FakeClock` past 5 s it is written again (`true`); a *different*
  signature within 5 s is written (per-signature, not global).
- **Never throws** on an unwritable path → returns without throwing.
- **Size cap:** seed a file past 512 KB; the next `Log` archives it to `tidsro.log.old` and the
  live file is small again.
- **Timestamps** use the injected clock (deterministic).

The WPF wiring (`e.Handled`, the three hooks, the balloon, `OpenLogFolder`) can't be instantiated
in xUnit — it stays a thin forwarding layer and is covered by manual acceptance, exactly as the
rest of `App.xaml.cs` is.

## Manual acceptance (gates release)

- Inject a deliberate throw into the 250 ms tick → the app stays alive, one calm tray balloon
  appears, and `%AppData%\Tidsro\tidsro.log` gets a timestamped entry. Revert the injected throw.
- Make that injected throw fire every tick → balloon + log entry appear **once**, not a flood; the
  app keeps running.
- Tray → **"Open log folder"** → Explorer opens at `%AppData%\Tidsro` with `tidsro.log` selected.
- Force the log past 512 KB → it rolls to `tidsro.log.old` and keeps going.
- (If the `MessageBox` fallback is kept) simulate a startup failure → the message names the log,
  an entry is written, the app exits cleanly.
- Screen-reader pass: the tray balloon is announced by Narrator (Windows toast).
