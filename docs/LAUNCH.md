# ShareSwitch — Product Hunt launch plan

Status key: ✅ ready · 🔲 to do · 👤 needs you (can't be done for you)

All paste-ready text (listing fields, maker comment, social posts) is in
[`LAUNCH-COPY.md`](LAUNCH-COPY.md).

---

## 1. Pick the date

| Option | Date (suggested) | Launch time in Singapore | Trade-off |
|---|---|---|---|
| **Weekend** | Sat 24 Oct 2026 | **3:01 PM SGT** | Less competition → a Top-3 badge is far more reachable. Your Twitch/YouTube viewers are around; LinkedIn is quiet. |
| **Weekday** | Tue 27 or Wed 28 Oct | **3:01 PM SGT** | Most traffic and newsletter exposure, but the hardest days to rank. LinkedIn and accelerator contacts are active. |

Product Hunt days reset at **12:01 AM Pacific**. Until 1 Nov that's PDT (= 3:01 PM
SGT); from 2 Nov it's PST (= **4:01 PM SGT**). Avoid Sun 1 Nov itself (clocks change).

**Recommendation:** with zero Product Hunt network and the goal of a badge for the
studio site, **Saturday 24 Oct**. It leaves one week to finish the materials and one
week to warm up your account. Bonus: 3 PM on a Saturday is a natural time to **launch
live on stream**.

---

## 2. App & release

- ✅ v0.6.1 code: rebrand, number shortcuts fix, installer header
- 👤 Test v0.6.1: number shortcuts (top row + numpad), installer pages, Settings
- 👤 Push, then tag `v0.6.1` → GitHub builds the installer + portable exe
- 👤 Install the released build on a second PC or a fresh Windows user account
  (catches the SmartScreen experience exactly as visitors will see it)
- 👤 Scan the released `.exe` on [virustotal.com](https://www.virustotal.com) and keep the
  link — the first "is this safe?" comment gets it as the reply
- 🔲 Decide on the GitHub name: keep `alexisdc06` or move the repo to an organisation
  (e.g. `acdesignstudio`) — old links redirect automatically. Tell me the name and
  I'll update every link. **Do this before the Framer page and listing are built.**
- 👤 GitHub repo → Settings: upload `assets/social-preview.png` (the new brand
  version) as the social preview; enable Issues; enable private vulnerability reporting
- 🔲 README: put the demo GIF at the top (I can do this)

## 3. Product Hunt listing assets

| Asset | Spec | File | Status |
|---|---|---|---|
| Thumbnail (looping GIF) | 240×240 | `launch/final/producthunt/thumbnail-240.gif` | ✅ |
| Thumbnail (static fallback) | 240×240 | `launch/final/producthunt/thumbnail-240.png` | ✅ |
| Gallery images | 1270×760 ×6 | `launch/final/producthunt/01–06*.png` (06 = saved numbers) | ✅ |
| Demo video | 16:9, 41 s, music + SFX | `launch/final/shareswitch-launch.mp4` | ✅ |
| Video **as a YouTube link** | Product Hunt's gallery takes a video URL, not a file | — | 👤 upload to your YouTube (public or unlisted) |
| Tagline, description, topics | ≤60 / ≤260 chars | `LAUNCH-COPY.md` | ✅ |
| Maker comment | pre-written | `LAUNCH-COPY.md` | ✅ |
| Website link | `https://ac-design.studio/shareswitch?ref=producthunt` | — | 🔲 after the Framer page is live |

- 👤 Create the listing and **schedule** it for 12:01 AM PT on the chosen day
- 👤 Copy the badge embed code Product Hunt gives you (for the website)

## 4. Website — `ac-design.studio/shareswitch`

- ✅ Source page: `site/index.html` (brand version, built for Framer import)
- ✅ Assets: `site/assets/` (backdrop, logo, demo loop, launch video, OG image)
- 👤 Push → GitHub Pages publishes it at `https://alexisdc06.github.io/ShareSwitch/`
- 👤 Build it in Framer (steps below)
- 👤 Paste the Product Hunt badge embed into the two marked slots (hero + final CTA)
- 👤 Point the launch-day bar's link at your Product Hunt page; remove it after launch week
- 👤 Page settings: title, description, OG image (`site/assets/og-image.png`)
- 👤 Link the page from the main ac-design.studio site (nav or work section)

**Getting it into Framer.** Framer has no "import HTML file". Two routes:

1. **Framer's AI agent:** give it the live GitHub Pages URL (or paste the HTML) and ask
   it to rebuild the page in your site's styles. Fastest; you then refine.
2. **Framer browser extension (official):** open the live page, copy one section at a
   time, paste onto the canvas as editable layers. Most faithful to the design.

Either way, afterwards: swap the two videos for Framer **Video** components (upload
`demo-loop.mp4` and `launch-video.mp4`), put the badge in an **Embed** component,
and check fonts (Figtree + Space Grotesk are both in Framer's Google Fonts list) and
the mobile breakpoint.

## 5. Warm-up week (the 7 days before)

- 👤 Complete your Product Hunt profile: photo, the bio below, link to ac-design.studio
- 👤 Leave 2–3 genuinely useful comments a day on relevant launches (design tools,
  Windows utilities, remote-work products). Quality over volume.
- 👤 Tell your network the **date** (no link yet) so anyone without a Product Hunt account
  can create one and look around before launch day. Brand-new accounts that only
  appear to vote look like spam to Product Hunt.
- 👤 Line up a short list of people who'll genuinely try it and comment — comments
  matter as much as votes.

> **Bio:** Industrial and UI/UX product designer. Founder of AC Design Studio and
> Threadworks. I bridge the gap between complex enterprise engineering and clean
> digital interfaces, and develop factory-ready technical soft goods. Building and
> prototyping in public on A Life of Design.

## 6. Launch-day run sheet (times in SGT, for a PDT launch day)

| Time | Action |
|---|---|
| 2:30 PM | Coffee. Download link, website and listing preview all checked once more. |
| **3:01 PM** | Listing goes live → post the **maker comment immediately**. |
| 3:05 PM | Go live on Twitch/YouTube: "launching ShareSwitch on Product Hunt right now". |
| 3:10 PM | LinkedIn post, X post, personal messages — ask for **support and feedback**, never "upvotes". |
| 3:00–7:00 PM | Reply to **every** comment, fast and thoughtfully. This window weighs most. |
| Evening | Post an update in the listing thread (e.g. a fix shipped, an answer to a common question). |
| Next day | Thank-you post with the result; put the badge on the studio site. |

## 7. Direct link or website?

**Both, for different jobs:**

- **Your network → straight to the Product Hunt page.** Voting and commenting happen
  there; every extra click loses people. Product Hunt doesn't publish how it treats
  referral sources. The documented risks are vote rings and new accounts that vote
  and vanish, not *where* people clicked from.
- **Product Hunt → your website.** The listing's website link should be
  `ac-design.studio/shareswitch?ref=producthunt`. That's how Product Hunt traffic lands
  on your studio domain (and shows up in your analytics), rather than on GitHub.
- **Your website → Product Hunt** (launch week only): the announcement bar and badge
  turn visitors arriving from search, socials or GitHub into Product Hunt visits.
