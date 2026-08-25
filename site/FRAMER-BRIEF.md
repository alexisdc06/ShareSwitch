# ShareSwitch landing page — Framer build brief

`index.html` in this folder is a working reference (structure + copy, not a design
you have to keep). Open it in a browser to see the flow. Rebuild it properly in
Framer — you'll do it better.

## Where to host it

**Recommended: a page on your existing site → `ac-design.studio/shareswitch`.**
Framer bills **per site**, so a separate ShareSwitch site — or a
`shareswitch.ac-design.studio` subdomain, which needs its own site — means a
second plan (~$10/mo). A new *page* on the studio site costs nothing, feeds
traffic to your domain, and doubles as a portfolio case study.

If you ever want a standalone domain (`shareswitch.app`), that's a new Framer
site plus the domain. Not worth it until the app has traction.

Free fallback if you'd rather not touch the studio site: GitHub Pages can serve
this HTML file from the repo at no cost.

## Sections, in order

| # | Section | Job |
|---|---|---|
| 1 | **Hero** | Headline, one-line explanation, download button, demo GIF |
| 2 | **How it works** | Three steps: share once → hold hotkey → click |
| 3 | **Shortcuts** | Table of key combos — shows depth without a features list |
| 4 | **Privacy** | Pre-empts "why does it need a keyboard hook?" |
| 5 | **FAQ** | SmartScreen, macOS, cursor, performance, app support |
| 6 | **Final CTA** | One button |
| 7 | **Footer** | MIT, AC Design Studio, GitHub, bug reports |

## Copy (edit freely — this is a first draft, not a spec)

**H1:** Change the window you're sharing. Without stopping the share.

**Sub:** Share one window in Teams, Zoom or Meet. Then hold a hotkey, click any
other window, and that's what everyone sees — instantly. No "let me re-share",
no dead air.

**Buttons:** Download for Windows · View source

**Under the buttons:** Free and open source · Windows 10 & 11 · ~5 MB · No
account, no telemetry

**Final CTA:** Stop saying "can you see my screen now?"

## The one thing that matters most

The **demo GIF in the hero**. This product is almost impossible to explain in
text and instantly obvious in motion. Everything above the fold exists to get
people to watch it. If you only polish one asset, polish that one.

Show the viewer's side of the call, so the "they never noticed" claim is visible
rather than asserted. Storyboard is in `../docs/LAUNCH.md`.

## Links to wire up

- Download → `https://github.com/alexisdc06/ShareSwitch/releases/latest`
- Source → `https://github.com/alexisdc06/ShareSwitch`
- Bugs → `https://github.com/alexisdc06/ShareSwitch/issues`

## Before publishing

- [ ] Page title + meta description (Framer → Page settings). Draft title:
      *ShareSwitch — Switch the window you're sharing, without stopping the share*
- [ ] OG image for link previews — reuse `../assets/social-preview.png` (1280×640)
- [ ] Check it on a phone: nobody installs a Windows app from their phone, but
      plenty of people will open the link there first
- [ ] Add the URL to the GitHub repo's About panel, and to the Product Hunt listing
