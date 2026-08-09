# ShareSwitch — Launch Plan (GitHub + Product Hunt)

Your PM checklist. Steps marked 🎨 are the design work only you can do;
everything else is either done or a 5-minute mechanical step.

---

## Phase A — Ship v0.2.0 (this week)

1. **Test the app** on your machine using `TESTING.md`. Send me any build errors or ❌ items — I fix, you re-test.
2. **Create the GitHub repo** (claude.ai/github or github.com → New → `ShareSwitch`, public, no README/license — we have them). Then from `C:\Projects\ShareSwitch\ShareScreen`:
   ```
   git remote add origin https://github.com/YOUR_USER/ShareSwitch.git
   git push -u origin main
   ```
3. **Tell me your GitHub username** — I'll replace the `REPLACE_GITHUB_USER` placeholders in README.md and installer/ShareSwitch.iss (or search-replace yourself).
4. **First release**: after CI is green,
   ```
   git tag v0.2.0
   git push origin v0.2.0
   ```
   → GitHub Actions builds the installer + portable exe and publishes the Release automatically. Download and test-install it once.

## Phase B — Design pass 🎨 (your focus)

The only assets standing between you and launch:

| Asset | Spec | Used in |
|---|---|---|
| 🎨 App icon | Replace `assets/icon.ico` (multi-size: 16–256 px) and `assets/icon.png` (256 px). Current one is my placeholder. | exe, installer, tray, taskbar |
| 🎨 Demo GIF | ~10 s, ≤1270×760, <10 MB. Storyboard below. | README hero + PH gallery |
| 🎨 PH thumbnail | 240×240 (animated GIF allowed — animated thumbnails get more clicks) | Product Hunt card |
| 🎨 PH gallery | 3–5 images, 1270×760. Suggested: ① hero with tagline ② "the problem" (stop-share dialog chaos) ③ hotkey+click flow ④ before/after comparison ⑤ "free & open source, MIT" | Product Hunt |
| 🎨 Social banner | 1280×640 (GitHub repo → Settings → Social preview; doubles as X/LinkedIn card) | GitHub + socials |

**Demo GIF storyboard** (this GIF sells the whole product):
1. A Meet/Teams call visible, sharing "ShareSwitch" window — viewer side shows a Figma file
2. Presenter holds Ctrl+Alt → blue frame glides across windows
3. Click a browser window → viewer side switches instantly
4. Caption: "Never stop sharing again."
Tip: record with ScreenToGif (free) at 1270×760; show the viewer's screen picture-in-picture so the "no interruption" claim is visible.

To rebuild with your icon: just replace the two files in `assets/`, commit, and tag a new version — CI does the rest.

## Phase C — Pre-launch polish (optional but high-ROI, tell me and I'll do it)

- Landing page on GitHub Pages (single HTML, your branding) — PH listings with a real site convert better than bare repo links
- Settings persistence + in-app autostart toggle (Phase 3 code)
- `winget` package submission (post-launch: gives you `winget install ShareSwitch` — great follow-up comment on PH)
- Code signing decision: an OV certificate (~€60–200/yr, e.g. Certum for open source) removes the SmartScreen warning. Not blocking, but the #1 complaint risk on PH. Alternative for later: sign via Azure Trusted Signing.

## Phase D — Product Hunt launch day

**Prep (done for you — copy below):** create the PH draft, schedule for **12:01 AM PT, Tuesday–Thursday**, add topics: `Windows`, `Productivity`, `Remote Work`, `Open Source`, `Meetings`.

**Tagline options (≤60 chars):**
- "Switch what you screen-share without stopping the share" ← recommended
- "Change your shared window mid-call. No one notices."
- "Hotkey-swap the window you're sharing in any video call"

**Description (≤260 chars):**
> ShareSwitch is a tiny free Windows app that mirrors any window into one shareable proxy window. Share it once in Teams/Zoom/Meet, then hold a hotkey and click any window to switch what viewers see — instantly, without ever stopping the share. Open source (MIT).

**First comment draft (post immediately after launch):**
> Hey Product Hunt! 👋
>
> I built ShareSwitch out of pure frustration: every design review I'd share a window, then need to show *another* window — stop share, find the right thumbnail, re-share, "can you see it now?"… momentum gone.
>
> ShareSwitch fixes it with one dumb-simple idea: you share a proxy window once, and a live mirror of any window appears inside it. Hold Ctrl+Alt, click any window, and that's what your viewers see. Instantly.
>
> It's free, open source (MIT), ~5 MB, no install required (portable exe available), built on the Windows compositor so there's zero performance cost.
>
> Things it doesn't do yet: show the mouse cursor (Windows API limitation I'm working around next), and macOS (sorry — Windows only for now).
>
> I'd love feedback — especially from people who present a lot. What would make this a daily driver for you?

**Launch-day rhythm:** reply to every comment within ~30 min for the first 12 h; post the link in relevant communities you're genuinely part of (no vote-begging — PH penalizes it); share on X/LinkedIn with the demo GIF around 8–9 AM in your audience's timezone.

**Success metrics for v1 launch:** GitHub stars + issues opened (real usage signal) > PH rank. A top-10 day for a niche Windows utility is a win.

---

## Timeline suggestion

| When | What |
|---|---|
| Week 1 | Test → fix loop; push to GitHub; v0.2.0 release; start icon design |
| Week 2 | 🎨 finalize icon + GIF + gallery; I do landing page + Phase 3 polish; re-release as v1.0.0 |
| Week 3 | Schedule PH for Tue/Wed 12:01 AM PT; prep socials; launch 🚀 |
