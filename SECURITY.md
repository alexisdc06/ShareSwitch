# Security & Privacy

## What ShareSwitch does with your data

**Nothing leaves your machine. The app contains no networking code at all** — no
telemetry, no analytics, no update check, no crash reporting. You can verify this:
search the source for `HttpClient`, `WebClient`, `Socket` — there are no matches.

## About the keyboard hook (read this — it's the honest part)

ShareSwitch installs a **global low-level keyboard hook** (`WH_KEYBOARD_LL`). This is
the same Windows mechanism a keylogger uses, so it deserves a straight explanation.

It is needed because the app must know you're holding the selection combo even while
another application has focus — that's the entire feature.

How the risk is contained, in code you can audit:

- `Hooks/KeyboardHook.cs` discards keys inside the hook callback itself, before
  the value is passed to any other code (see the `IsRelevantKey` filter and the
  `PRIVACY BOUNDARY` comment). Only two categories survive:
  - **Ctrl, Alt, Shift and Escape** — always, since they drive selection mode.
  - **Digits 1–9 and the ← / → arrows** — *only while a modifier key is
    physically held*, because they trigger the slot and cycle shortcuts. When
    you are typing normally, with no modifier down, they are discarded like
    everything else.
- No other key ever leaves the hook. Letters, numbers typed without a modifier,
  passwords, and clipboard contents are never observed.
- Nothing is written to disk, buffered in memory, or transmitted.
- The mouse hook (`WH_MOUSE_LL`) is installed **only while you hold the combo** and
  uninstalled the moment you release it.
- Both hooks are removed on exit; the app requires no administrator privileges.

If you'd rather not run an app with a keyboard hook at all, you can still use
ShareSwitch entirely from the tray menu (**Select Window…**) — the feature degrades
gracefully.

## Antivirus / SmartScreen

Releases are not code-signed yet, so Windows SmartScreen will warn on first run, and
some antivirus engines flag *any* unsigned binary that installs input hooks. Both are
heuristics, not detections of actual malicious behavior. If you're cautious, build
from source — it takes one command (`dotnet build`) and the result is identical.

## Reporting a vulnerability

Please report privately via GitHub: **Security → Report a vulnerability** on this
repository (GitHub private vulnerability reporting). Please don't open a public issue
for a security problem.

Expect an initial response within a few days. This is a small side project maintained
by one person — fixes are best-effort, but security reports get priority.

## Scope

In scope: privilege escalation, arbitrary code execution, anything that causes
ShareSwitch to leak captured window content or keystrokes off the machine, or that
lets another process abuse ShareSwitch's hooks.

Out of scope: SmartScreen warnings on unsigned builds, antivirus false positives, and
the documented DWM limitations (protected windows can't be captured — that's Windows
enforcing DRM, working as intended).
