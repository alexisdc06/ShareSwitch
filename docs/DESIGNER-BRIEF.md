# ShareSwitch — Design Brief

## What it is

A small free Windows app for people who share their screen in video calls.

**The problem:** you're sharing one window in Teams/Zoom/Meet, you need to show a
different one, so you stop sharing, find the right window, share again — and the
meeting waits.

**What it does:** ShareSwitch opens one window that mirrors any other window. You
share *that* window once. Then hold `Ctrl+Alt`, click any window on your screen,
and your viewers instantly see it instead. Nothing is interrupted.

**Who uses it:** anyone who presents a lot — designers, PMs, teachers, consultants.
Mostly on Windows, mostly technical-ish, not beginners.

**Feel:** fast, quiet, trustworthy. It's a utility that sits in the system tray, not
a consumer app. Closer to a good developer tool than to a SaaS landing page.

Current visuals are placeholders I made — ignore them entirely, no attachment.

---

## Phase 1 — Branding

**Goal:** a minimal identity we can apply everywhere. Not a full brand book.

Deliverables:

- **Logo / app icon** — one mark, works alone. *Must stay readable at 16×16 px*
  (it lives in the Windows system tray next to the clock). This is the hard
  constraint — test it tiny before falling in love with it.
- **Wordmark** — "ShareSwitch", one lockup with the mark.
- **Colors** — 1 accent + dark UI neutrals. The accent is used for a highlight
  frame drawn around windows on screen, so it must be visible over *any*
  background (photos, white docs, dark code editors).
- **Type** — one display + one UI face. UI text has to render at small sizes.
- **One-page summary** of the above.

Current placeholder accent is `#00AAFF` on near-black. Change it if you want.

---

## Phase 2 — Assets

Once Phase 1 is approved. Exact specs:

| Asset | Format | Notes |
|---|---|---|
| App icon | `.ico`, multi-size 16/24/32/48/64/128/256 px + 256px `.png` | Windows taskbar, tray, installer |
| App backdrop | 1920×1080 `.png` | Shown inside the app before a window is picked — viewers on the call see this |
| Demo GIF | ≤1270×760, <10 MB, ~10 s, loops | **Most important asset** — see below |
| Social banner | 1280×640 `.png` | Link previews (WhatsApp, Slack, LinkedIn) |
| Product Hunt thumbnail | 240×240, GIF allowed | |
| Product Hunt gallery | 3–5 × 1270×760 | Hero, the problem, the gesture, before/after |
| Landing page | Framer, page on ac-design.studio | Copy + structure already written |

### The demo GIF matters most

This product is impossible to explain in words and obvious in motion. Sequence:

1. A call is running, screen being shared
2. Presenter holds `Ctrl+Alt` — a highlight frame follows the cursor across windows
3. Click → the viewers' screen switches instantly
4. End card: "Never stop sharing again."

Show the **viewer's side** picture-in-picture, so the "nobody noticed" claim is
something people see rather than something we say.

---

## Practical

- Delivery: source files + exports. Drop into `assets/` in the project.
- The app icon and backdrop get compiled into the app, so they need to be final-ish
  before a release; everything else can iterate.
- Anything unclear about how the app behaves, ask — happy to screen-record it.
