# ShareSwitch page: Framer build brief

`index.html` is the reference build for `ac-design.studio/shareswitch`. Open it in a
browser to see the flow, then rebuild or import it into the studio site.

## The page lives inside the studio

```
Studio Nav          <- the studio's own Nav component (scrolls away)
ShareSwitch bar     <- sticky: Lab / ShareSwitch, section links, Download
ShareSwitch content <- hero, facts, how it works, demo, shortcuts, privacy, FAQ, CTA
Studio Footer       <- the studio's own Footer component
```

The studio nav and footer let visitors explore the studio. The sticky ShareSwitch bar
is what stops them getting lost on this page: wherever they scroll, the product name,
the sections and the download button stay one tap away. The studio nav and footer in
`index.html` are stand-ins. In Framer, delete them and drop in the real components.

## Making the page easy to come back to

People who leave for the studio pages need a road back. Three small edits on the
studio site do it:

1. **Lab page**: add ShareSwitch as a card in the working-products part of `/lab`.
   This is where it belongs, and the bar's "Lab" link already points there.
2. **Studio footer**: add a "Products" column (or a line under Lab) with
   **ShareSwitch** linking to `/shareswitch`. Every studio page then links back.
3. **Studio menu**: optional, add ShareSwitch under Lab in the hamburger menu.

## Sections, in order

| # | Section | Job |
|---|---|---|
| 1 | Hero | Headline, one sentence, download button, demo loop |
| 2 | Facts | Free, Windows 10 & 11, no account, no telemetry, Product Hunt badge |
| 3 | How it works | Three ordered steps next to a sticky intro |
| 4 | Demo | The launch video |
| 5 | Shortcuts | Key combos plus a real capture of a saved window |
| 6 | Privacy | Answers "why does it need a keyboard hook?" |
| 7 | FAQ | SmartScreen, app support, cursor, performance, macOS |
| 8 | Final CTA | Download and GitHub |

## House rules for this page

- No em dashes or en dashes anywhere, including titles and meta descriptions.
- No small uppercase labels above headings. The heading does the work.
- One label per action: the download button always reads "Download for Windows".
- Radius rule: buttons are full pills, media is 16px, keycaps are 8px.

## Links

- Download: `https://github.com/alexisdc06/ShareSwitch/releases/latest`
- Source: `https://github.com/alexisdc06/ShareSwitch`
- Bugs: `https://github.com/alexisdc06/ShareSwitch/issues`

## Before publishing

- [ ] Page title and meta description (Framer, Page settings). Use the ones in
      `index.html`.
- [ ] OG image: `assets/og-image.png`
- [ ] Upload `demo-loop.mp4` and `launch-video.mp4` as Framer Video components
- [ ] Paste the Product Hunt badge embed in both badge slots
- [ ] Check it on a phone
- [ ] Add the URL to the GitHub repo's About panel and to the Product Hunt listing
