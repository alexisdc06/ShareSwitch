# Changelog

## v0.6.1 — 2026-10-10

**Fixed**

- **Number shortcuts** (`Ctrl+Alt+Shift+1…9` to assign, `Ctrl+Alt+1…9` to
  recall) now recognise digits by key position. They work on every keyboard
  layout and on the numpad — where Windows used to turn Shift + a numpad digit
  into End/Down/PgDn, so the assignment never arrived.
- Holding **Ctrl+Alt+Shift** now shows the highlight frame too, labelled
  *Press 1–9 to save*, so you can see which window you're about to assign.
  Saving or recalling flashes *Saved to 3 · Window* / *3 · Window* as
  confirmation.
- Unexpected errors inside the input hooks are now written to
  `%APPDATA%\ShareSwitch\errors.log` instead of vanishing silently (exception
  details only — never keys or window content).

**Changed**

- **Installer**: the page header is a taller navy band with the title and
  description centred vertically, a larger app icon, and more space above the
  page content.

## v0.6.0 — 2026-10-10

**The interface now wears the ShareSwitch brand.**

- **Share window**: the idle screen — what a call sees before you pick a window —
  is a branded title card on the brand gradient with the oversized glass SS:
  logo, *Share this window in Teams, Zoom or Meet*, and the gesture drawn as
  keycaps. It scales cleanly from the 480×270 default up to full screen.
- **Accent colour** is the brand violet (#9085FF) everywhere: title bar, window
  border, the selection frame and its title chip.
- **Settings** is a dark two-column window with a brand header, toggle
  switches, a branded hotkey picker and a cleaner list of assigned windows.
- **Window picker**: dark, two-line rows (window title + app), pill buttons.
- **Tray menu**: navy with violet highlights; the status line reads in violet.
- **App icon**: the designer's icon, with hand-tuned 16–32 px versions so it
  stays crisp in the tray.
- **Installer**: brand side panel and header image.
- Typography uses Figtree (SIL Open Font License, bundled) as a stand-in for
  Aeonik, which is licensed separately.

## v0.5.0 — 2026-08-22

**Added**

- **Start minimized to the tray** (Settings → General). Pairs with *Start with
  Windows* so ShareSwitch can sit quietly in the tray until you need it, instead
  of throwing a window onto your desktop at every login. A one-off balloon tip
  points at the tray icon so the app is never invisible.
- The share window now **remembers where you put it**. Size is remembered too
  when auto-fit is off; with auto-fit on, size belongs to whatever is being
  mirrored. A saved position on a monitor you've since unplugged is recovered
  back onto the primary screen.
- **Branded backdrop** behind the mirror, visible before a source is picked and
  behind the letterbox bars. Replace `assets/background.png` and rebuild to
  change it, or point `BackgroundImagePath` at your own file.
- The idle window is now a proper title card — what your viewers see if you
  share before picking a source — telling you to share this window, the hotkey
  to use, and where the settings live.

**Changed**

- Default window size is now 480×270 instead of 1280×720. Auto-fit resizes it
  the moment you pick a source, so a large default only got in the way.

## v0.4.0 — 2026-08-21

**Added**

- **Settings window** (tray → *Settings…*), including a **Start with Windows**
  toggle. Preferences now persist in `%APPDATA%\ShareSwitch\settings.json`.
- **Number shortcuts**: `Ctrl+Alt+Shift+1…9` assigns the window under your
  cursor to a number; `Ctrl+Alt+1…9` then switches to it instantly from
  anywhere — no need to focus ShareSwitch first.
- **Cycle shortcuts**: `Ctrl+Alt+←` / `→` steps through open windows. The list
  is snapshotted when you start cycling, so it can't reorder under you mid-call.
- Both shortcut families can be turned off in Settings.

**Changed**

- Tray menu simplified — options moved into the Settings window.
- The keyboard hook now also observes digits and arrows, but **only while a
  modifier is physically held**; ordinary typing is still discarded inside the
  hook. See `SECURITY.md`.

**Design note**

Settings deliberately live in their own window rather than a menu bar inside the
share window: that window's content is exactly what your viewers see, so it stays
pure content forever.

## v0.3.0 — 2026-08-21

First version shared for real-world testing.

**Added**

- **Auto-fit**: the ShareSwitch window now resizes itself to match the mirrored
  window's shape, and keeps tracking it while you resize the source. No more
  black letterbox bars for your viewers. Toggle in tray → *Auto-fit window to source*.
- **Visible identity**: the ShareSwitch window gets an accent-blue title bar and
  border so it can't be confused with the app it's mirroring. On Windows 11 this
  lives in the window chrome, which screen-sharing does **not** capture — your
  viewers only ever see clean content. (Windows 10 falls back to a thin in-content frame.)
- Tray menu and tooltip now show what's currently being mirrored.
- `SECURITY.md` documenting the keyboard hook, the privacy boundary, and how to
  report a vulnerability.
- CodeQL security scanning and Dependabot on the repository.

**Changed**

- The selection highlight now **fades out** after you pick a window instead of
  staying on screen.
- The highlight is driven by a UI timer rather than the mouse hook callback, so
  the hook stays fast and can't be dropped by Windows.
- The keyboard hook now discards every key that isn't Ctrl / Alt / Shift / Escape
  *inside the hook itself*, so no other code can ever observe what you type.

**Fixed**

- The blue highlight frame could stay stuck on screen after selecting a window
  (and survive closing the call), forcing a hard exit. The overlay was being
  shown with a raw Win32 call behind WinForms' back, so `Hide()` silently did
  nothing. It now shows through WinForms and hides reliably, with a lifetime
  failsafe as backup.

**Known limitations**

- The mouse cursor is not shown in the mirror (DWM limitation).
- Protected windows (DRM video, some banking apps) and some UWP apps can't be mirrored.
- Minimized source windows freeze until restored.
- Builds are not code-signed yet, so Windows SmartScreen warns on first run.

## v0.2.0

- Phase 2 foundation: hold-to-select with global hotkey, window picker, tray app,
  DWM thumbnail mirroring, installer and automated releases.
