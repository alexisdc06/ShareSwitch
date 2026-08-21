# ShareSwitch — Phase 2 Test SOP

Run on the Windows 11 machine. Tests are ordered; later ones assume earlier ones pass.
Mark each ✅ / ❌ and note anything odd — this list goes to the next polishing pass.

## 0. Build & launch

1. Open `ShareSwitch.sln` in Visual Studio 2022 → F5.
   - ✅ Builds with no errors, tray icon appears, ShareWindow opens showing the
     "No source selected" hint with the current hotkey (Ctrl + Alt).
2. Launch the .exe a second time while running.
   - ✅ "Already running" message, no second instance.

## 1. Window picker (no hooks involved)

3. Tray → **Select Window…**
   - ✅ List shows your normal app windows with process names; no taskbar/desktop/hidden junk; ShareSwitch itself absent.
4. Select Notepad (or any app) → **Select**.
   - ✅ Live mirror appears; the window resizes to match the source's shape with no black bars.
   - ✅ Title bar reads "● ShareSwitch — mirroring: …" and is tinted blue (Win11) / a thin blue frame appears (Win10).
   - ✅ Tray tooltip and the top line of the tray menu show the same source.
5. Resize the **source** window by dragging its edges.
   - ✅ ShareSwitch follows within ~½ s, keeping the same shape, no stretching.
6. Turn off tray → **Auto-fit window to source**, then resize ShareSwitch manually.
   - ✅ It stays where you put it; mirror letterboxes inside, aspect preserved.
7. Type in the source app.
   - ✅ Mirror updates live.

## 2. Hold-to-select (hooks)

7. Hold **Ctrl + Alt** (don't click yet) and move the mouse across different windows.
   - ✅ After ~150 ms a blue frame + title chip follows the window under the cursor.
   - ✅ Frame matches the window's visible edges (no offset — check on your scaling, e.g. 125 %/150 %).
   - ✅ Desktop and taskbar get no frame.
8. Still holding, **left-click** a window.
   - ✅ ShareWindow switches instantly; the frame **fades out** over ~¼ s and is gone.
   - ✅ Nothing is left on screen afterwards — check again after closing the call.
   - ✅ The clicked app did NOT receive the click (e.g. hover a button — it must not be pressed) and was not focused/raised.
9. Hold Ctrl + Alt, then **release without clicking**.
   - ✅ Frame disappears, nothing switches.
10. Hold Ctrl + Alt, press **Esc**.
    - ✅ Selection cancels; the focused app did not react to Esc.
11. Hold Ctrl + Alt and click **the ShareWindow itself / desktop / taskbar**.
    - ✅ Nothing switches; click behaves normally (not swallowed).
12. Press app shortcuts using **Ctrl + Alt + Shift + something** while combo = Ctrl + Alt.
    - ✅ Selection mode does NOT engage (exact-match rule).
13. AZERTY check: type `@ # { [` (AltGr) in any editor at normal speed.
    - ✅ Characters typed normally; at most a brief frame flash if AltGr held long.

## 2b. Slots and cycling

14. Hold **Ctrl+Alt+Shift**, hover a window, press **1**. Repeat with another window and **2**.
    - ✅ Each press switches the mirror and flashes the frame on that window.
15. Release everything. Now press **Ctrl+Alt+1**, then **Ctrl+Alt+2**, while focused in a *different* app.
    - ✅ Mirror jumps between the two assigned windows; the focused app never receives the digit.
16. Press **Ctrl+Alt+→** several times, then **←**.
    - ✅ Steps through open windows; order stays consistent within one hold, even if you open/close an app meanwhile.
17. Close one of the assigned apps, then press its number.
    - ✅ Nothing happens (dead slot ignored, no crash); the slot shows as unassigned in Settings.
18. Type normally in an editor: digits, arrows, passwords.
    - ✅ Everything types correctly — shortcuts only bind while the modifiers are held.

## 2c. Settings and startup

19. Tray → **Settings…**
    - ✅ Opens a separate window (never inside the share window); shows assigned slots 1–9.
20. Tick **Start ShareSwitch when Windows starts**, close, reopen Settings.
    - ✅ Still ticked. (Verify in Task Manager → Startup, or `shell:startup` registry Run key.)
21. Change the hotkey, close the app entirely, relaunch.
    - ✅ Setting persisted (`%APPDATA%\ShareSwitch\settings.json`).
22. Untick both shortcut families.
    - ✅ Number/arrow shortcuts stop working; hold-and-click still works.

## 3. Hotkey configuration

14. Tray → **Selection Hotkey** → Ctrl + Shift.
    - ✅ Menu check moves; hint text in ShareWindow (when no source) shows new combo.
    - ✅ Ctrl + Shift now triggers selection; Ctrl + Alt no longer does.
15. Switch back to Ctrl + Alt.

## 4. Lifecycle & robustness

16. Mirror a window, then **close the source app**.
    - ✅ Within ~1 s ShareWindow shows "The source window was closed." + hint.
17. Mirror a window, **minimize the source**.
    - Expected limitation: content freezes/blanks (DWM). Restore → live again.
18. Click ShareWindow's **X**.
    - ✅ Window hides (tray double-click brings it back, mirror still intact).
19. Try mirroring a protected window if available (e.g. a DRM video player).
    - ✅ Warning dialog, app keeps running.
20. Tray → **Exit**.
    - ✅ Tray icon gone, process gone (check Task Manager), no error dialogs.
    - ✅ After exit, keyboard/mouse behave normally everywhere (hooks released).

## 5. The real thing — video call

21. Start a Teams/Zoom/Meet call (two devices or a colleague). Share → Window → **ShareSwitch**.
    - ✅ Viewer sees the mirrored content.
22. During the share, hold Ctrl + Alt and click a different window.
    - ✅ Viewer's content switches with no re-share, no black frame, no yellow border.
23. Switch sources 5–10 times rapidly; resize ShareWindow mid-call.
    - ✅ Stable, no crash, no DWM artifacts, CPU stays low (check Task Manager — should be ~0 % idle).

## 6. Multi-monitor / DPI (if applicable)

24. Repeat tests 7–8 on a second monitor, ideally with different scaling.
    - ✅ Highlight frame aligns; switching works across monitors.

## Known accepted limitations (don't report as bugs)

- No cursor in the mirror; minimized sources freeze; some UWP/protected windows refuse capture.
- Windows fully behind ShareWindow can't be hover-picked while the "no source" hint is shown — use the tray picker.
