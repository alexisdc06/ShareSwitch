# ShareSwitch — Design Brief

Everything here is currently a **placeholder I generated**. Replace any file and the
app/repo picks it up — no code changes needed. Exact specs and drop-in paths below.

## Brand starting point (change freely)

| Token | Current | Where it's used |
|---|---|---|
| Accent | `#00AAFF` | selection highlight frame, icon, banner |
| Surface | `#111A` / `#1A1F28` | ShareWindow letterbox, banner |
| Text | `#F0F4F8` / muted `#8C96A5` | banner |

The accent color also lives in code — if you change it, tell me and I'll update
`Selection/HighlightOverlay.cs` (the frame users see when picking a window).

## Assets to design

### 1. App icon — highest priority
- **Files to replace:** `assets/icon.ico` **and** `assets/icon.png`
- `icon.ico` must be **multi-size**: 16, 24, 32, 48, 64, 128, 256 px in one file
  (export from Figma/Illustrator as PNGs → convert at [icoconvert.com](https://icoconvert.com) or `magick convert`)
- `icon.png` = 256×256, transparent background
- Appears at: taskbar, system tray (16–24 px — **test legibility tiny**), installer, .exe in Explorer, README header
- Design note: at 16 px the current two-window concept nearly dissolves. A single
  bold mark (arrow-through-frame, swap glyph) may read better.

### 2. Social preview — fixes the ugly link previews
- **File:** `assets/social-preview.png` — 1280×640, PNG under 1 MB
- Must also be **uploaded manually once**: repo → Settings → scroll to "Social preview" → Upload
  (GitHub doesn't read it from the repo; this is the only asset needing manual upload)
- Shows on: WhatsApp/Slack/X/LinkedIn link previews
- Safe zone: keep text within the central 1100×500 — some clients crop edges

### 3. Demo GIF — the thing that actually sells it
- **File:** `assets/demo.gif` — ≤1270×760, under 10 MB, ~8–12 s, loops
- Then uncomment the two lines in `README.md` under `<!-- DESIGNER: hero demo GIF -->`
- Storyboard: call visible sharing "ShareSwitch" → hold Ctrl+Alt → frame glides over windows → click → viewer content switches instantly
- Record with [ScreenToGif](https://www.screentogif.com/) (free). Show the *viewer's* side picture-in-picture — that's the proof.

### 4. Product Hunt gallery — later, before launch day
- Thumbnail 240×240 (animated GIF allowed, gets more clicks)
- 3–5 gallery images at 1270×760: ① hero + tagline ② the problem ③ hotkey+click flow ④ before/after ⑤ free & open source

## In-app design (tell me and I'll implement)

The app itself is functional-ugly right now — default WinForms. Candidates:

- **Empty state** of ShareWindow: currently grey text on black. Could be a proper centered card with the icon, the hotkey shown as keycaps, and a "Pick a window" button.
- **Highlight frame**: solid 4 px blue rectangle + title chip top-left. Could get rounded corners, drop shadow, animated pulse, corner brackets instead of full frame.
- **Window picker**: plain ListView. Could show app icons + live thumbnails per window.
- **Tray menu**: default Windows menu. Could show the current source name at top.

Send me a mockup or just describe it — this is the layer where the product feels designed rather than assembled.

## Workflow for shipping design changes

1. Replace files in `assets/`
2. GitHub Desktop → commit → Push
3. For it to reach users, cut a release (see `docs/LAUNCH.md` Phase A step 4)
