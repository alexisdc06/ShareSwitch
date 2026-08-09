# Development notes

## Build & run

- Open `ShareSwitch.sln` in Visual Studio 2022 → F5, or `dotnet build`.
- Distributable build: `.\publish.ps1 -Version x.y.z` → portable exe in `publish\`,
  installer in `installer\Output\` (needs Inno Setup 6: `winget install JRSoftware.InnoSetup`).
- Releases are automated: push a `vX.Y.Z` tag and `.github/workflows/release.yml`
  builds both artifacts and attaches them to a GitHub Release.

## Project structure

```
Program.cs                       Entry point, single-instance mutex, crash dialog
TrayApplicationContext.cs        App root: tray icon/menu, owns ShareWindow + SelectionController
ShareWindow.cs                   Shared proxy window; letterboxed DWM thumbnail; hide-on-close;
                                 click-through (HTTRANSPARENT) during selection; source watchdog
WindowPickerForm.cs              Modal window list (title + process, refresh)
WindowEnumerator.cs              Shared filtering rules + window-under-point resolution
DwmThumbnail.cs                  Safe DWM thumbnail wrapper (register/size/letterbox/cleanup)
Settings.cs                      In-memory settings (persistence lands here)
AppIcon.cs                       Icon extraction from the exe, with fallback
Interop/NativeMethods.cs         Shared user32/dwmapi P/Invoke
Hooks/KeyboardHook.cs            Global WH_KEYBOARD_LL (always installed)
Hooks/MouseHook.cs               Global WH_MOUSE_LL (installed only during selection)
Selection/SelectionController.cs Hold-to-select state machine
Selection/HighlightOverlay.cs    Topmost click-through highlight frame + title chip
```

## Design decisions

- **DWM thumbnails** (not Windows.Graphics.Capture): zero-copy compositor mirror,
  no yellow capture border, trivial resource model. Trade-off: no cursor, some
  protected/UWP windows refuse capture.
- **Click-through** uses `WM_NCHITTEST → HTTRANSPARENT` rather than
  `WS_EX_LAYERED|WS_EX_TRANSPARENT`, because layered destination windows can break
  DWM thumbnail rendering. `WindowFromPoint` honors HTTRANSPARENT for same-thread
  windows, which is exactly our case.
- **AltGr guard**: on AZERTY layouts AltGr reports as Ctrl+Alt, so selection mode
  only engages after the combo is held ~150 ms (`AppSettings.SelectionActivationDelayMs`),
  and entering requires an *exact* modifier match.
- **Hooks**: keyboard hook is permanent; mouse hook exists only while selecting.
  Callbacks never throw, delegates are GC-rooted, everything is IDisposable.
  If hook installation fails, the tray picker still works.
- The selecting left-click (down **and** up) is swallowed so the target app never sees it.
- ShareWindow re-registers its thumbnail if WinForms recreates its handle; a 1 s
  watchdog clears the view when the source window is closed; the X button hides
  instead of closing so a live call share is never killed accidentally.

## Testing

Manual test protocol: [`../TESTING.md`](../TESTING.md).

## Roadmap (Phase 3)

- Settings persistence (JSON in `%APPDATA%\ShareSwitch`) — extend `AppSettings`
- In-app auto-start toggle (installer already offers a startup task)
- Custom cursor rendering research; multi-source presets; polish
