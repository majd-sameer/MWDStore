# Build Spec — Khalid Saleh QA Portfolio (Next.js)

A handoff spec for **Claude Code** to recreate the existing HTML prototype (`Portfolio Bold.dc.html`) as a production **Next.js** app. Reproduce the design **exactly** — same layout, tokens, type, spacing, animations, and copy. Do not redesign.

Concept: a QA engineer's portfolio framed as a **passing test report** — monospace "terminal" accents, huge display type, expandable job "test suites", light/dark mode, scroll-progress bar, and a tools marquee.

---

## 1. Stack & setup

- **Next.js 14+** (App Router), **TypeScript**, **React 18**.
- **Tailwind CSS** for styling. Use CSS variables (below) for theme tokens; drive dark mode with a `dark` class on `<body>` (Tailwind `darkMode: 'class'`).
- No UI component library. Plain React + Tailwind.
- Fonts via `next/font/google`: **Space Grotesk** (400/500/600/700) as the display/body font, **IBM Plex Mono** (400/500 + italic 400) as the mono font. Expose them as CSS variables `--font-space` and `--font-mono`.
- Deploy target: static/SSG is fine (`output: 'export'` acceptable). No backend, no DB.

```bash
npx create-next-app@latest portfolio --typescript --tailwind --app --eslint
```

---

## 2. Design tokens (CSS variables)

Define in `app/globals.css`. **Light is default; `body.dark` overrides.** These values are exact — copy them.

```css
:root {
  --bg: #F3F2EC;
  --panel: #FBFAF5;
  --text: #17160F;
  --muted: #6E6C60;
  --faint: #A5A398;
  --line: #DEDCD1;
  --accent: oklch(0.60 0.15 150);      /* signal green */
  --accent-ink: #0B2416;               /* text on accent fills */
  --soft: oklch(0.60 0.15 150 / 0.12); /* accent tint */
}
body.dark {
  --bg: #0D0E0B;
  --panel: #16170F;
  --text: #ECEBE1;
  --muted: #93958A;
  --faint: #5E5F55;
  --line: #262820;
  --accent: oklch(0.82 0.17 148);
  --accent-ink: #08160D;
  --soft: oklch(0.82 0.17 148 / 0.14);
}
html { scroll-behavior: smooth; }
body {
  margin: 0;
  background: var(--bg);
  color: var(--text);
  font-family: var(--font-space), Helvetica, Arial, sans-serif;
  -webkit-font-smoothing: antialiased;
  transition: background .3s ease, color .3s ease;
}
a { color: var(--accent); text-decoration: none; }
a:hover { opacity: .7; }
::selection { background: var(--accent); color: var(--accent-ink); }
.mono { font-family: var(--font-mono), monospace; }

@keyframes blink   { 0%,49%{opacity:1} 50%,100%{opacity:0} }
@keyframes marquee { from{transform:translateX(0)} to{transform:translateX(-50%)} }
@keyframes fadeUp  { from{opacity:0;transform:translateY(8px)} to{opacity:1;transform:translateY(0)} }
```

Map tokens into Tailwind via `theme.extend.colors` (e.g. `bg: 'var(--bg)'`, `panel`, `text`, `muted`, `faint`, `line`, `accent`, `accentInk: 'var(--accent-ink)'`, `soft`) so you can write `bg-panel border-line text-muted` etc. Alternatively use arbitrary values `bg-[var(--panel)]`. Either is fine — keep it consistent.

### Accent theming rule (important)
The accent can be re-tinted. When an accent hex is chosen, set it on `document.body.style` (not `:root`) so it beats the `body.dark` rule, and **lighten it in dark mode**:
```js
const eff = isDark ? `color-mix(in oklab, ${hex} 68%, white)` : hex;
document.body.style.setProperty('--accent', eff);
document.body.style.setProperty('--soft', `color-mix(in srgb, ${eff} 12%, transparent)`);
```
Accent palette options (swatches): `#3E9B63` (default green), `#C6603A` (rust), `#4A66D6` (blue), `#8C5B5B` (rose).

---

## 3. Layout constants

- Content max-width: **1080px**, centered, horizontal padding **32px** (`max-w-[1080px] mx-auto px-8`).
- Section vertical rhythm: ~**76px** top padding on major sections.
- Corner radii: cards **12px**, buttons **6px**, monogram tiles **10px**, small tags **4–5px**, chips **999px**.
- Borders: **1px solid var(--line)** everywhere.
- Type scale (use `clamp` exactly as given):
    - Hero H1: `clamp(52px, 9vw, 128px)`, weight 700, line-height .92, letter-spacing -.03em
    - Section H2: `clamp(30px, 4vw, 44px)`, weight 700, letter-spacing -.02em
    - Contact H2: `clamp(34px, 6vw, 68px)`, line-height 1.02
    - Body: 15–16px, muted color, line-height ~1.55–1.6
    - Mono labels: 11–13px, letter-spacing .02–.12em, often UPPERCASE

---

## 4. Component / section structure

Build one page (`app/page.tsx`) composed of these sections in order. Split into components under `components/`. Content data lives in `lib/data.ts` (typed).

1. **ScrollProgressBar** — fixed top, `height:3px`, full width, `z-60`. Inner bar width = scroll %; `background: var(--accent)`; `transition: width .1s linear`. Compute on window `scroll` (passive listener): `pct = scrollTop / (scrollHeight - clientHeight) * 100`.

2. **StatusBar** (sticky top, `z-50`, `bg-panel`, bottom border, `backdrop-blur`) — mono, height 46px. Left: a pulsing accent dot (`box-shadow: 0 0 0 3px var(--soft)`) + `all suites: PASS` + `· 19 yrs uptime · Amman, JO`. Right: anchor links `./suites`, `./companies`, `./stack`, `./contact` + a **theme toggle button** (bordered, radius 4px) showing `☾ dark` / `☀ light`.

3. **Hero** — mono eyebrow `$ whoami` with a **blinking block cursor** (`.cursor` span, `animation: blink 1.1s steps(1) infinite`, sized `width:.6em;height:1.05em;background:var(--accent)`). Grid `1fr auto`: left = H1 `Khalid<br>Saleh`, sub-paragraph, and two buttons (`→ hire me` = accent fill with `--accent-ink` text; `↓ download.cv` = outlined, links to the CV PDF in `/public`). Right = a photo frame `190×240`, radius 10, `bg: var(--soft)`, 1px border, **hidden below 720px viewport** (only shown on wider screens); caption `fig.01 — subject` bottom-right in mono/faint. Use a Next `<Image>` placeholder or a drop-in `/public/portrait.jpg`.

4. **StatReport strip** (full-width band, `bg-panel`, top+bottom border) — responsive grid `repeat(auto-fit, minmax(160px,1fr))`, each cell has a **left border** (`border-l`, `-ml-px`), a big 42px/700 number and a mono caption. Data:
    - `19+` — years in QA
    - `7` — roles / suites passed
    - `4` — QA teams led
    - `2` — countries — JO · UAE

5. **Suites (Experience)** `#suites` — header row: H2 "Test suites" + mono note `7 passed / 7 total · click to expand`. Then a list of **7 expandable role rows** (`roles` data). Each row: grid `76px 1fr auto`, top border, `cursor:pointer`, click toggles expansion.
    - Col 1: mono index `TC-01`…`TC-07` (faint).
    - Col 2: title (600, `clamp(20px,2.6vw,26px)`) + mono `@ Company` (accent); mono meta line `period · duration · location`; an italic-ish muted `about` paragraph (max-width 62ch). When expanded, an **assertions** block (mono uppercase accent label) renders the `points` as a list where each item is grid `22px 1fr` with an accent `✓` glyph + text; wrap the block with `animation: fadeUp .28s ease`.
    - Col 3: a **PASS badge** (mono, accent text, 1px accent border, `bg: var(--soft)`, radius 5) and a chevron `▾` that rotates 180° when expanded (`transition: transform .25s`).
    - Default state: **first row (index 0) expanded**.
    - A `experienceDetail` toggle (`condensed` | `brief`) controls bullet count: `brief` shows only the first 2 points per role. Default `condensed` (all points).

6. **Marquee** (full-width band, `bg-panel`, top+bottom border, `overflow:hidden`, `py-5`) — a mono row of tools, `width:max-content`, `animation: marquee 26s linear infinite`. **Duplicate the list twice** in the DOM so the -50% translate loops seamlessly. Pause on hover (`.track:hover .marq { animation-play-state: paused }`). Each item: `Selenium <accent>/</accent>` etc.

7. **Companies** `#where` — header: H2 "Where I've worked" + mono note `4 employers · 3 international clients · tap to view profile`. A grid `repeat(auto-fit, minmax(280px,1fr))`, gap 16 of **company cards** — each an `<a target="_blank" rel="noopener">` to the company URL, `bg-panel`, 1px border, radius 12, padding 24, hover: `border-color: var(--accent); transform: translateY(-3px)`. Card contents:
    - Row: **monogram tile** (52×52, radius 10, `bg: var(--soft)`, 1px border, mono 17px/600 accent text) + sector **tags** (mono, uppercase, accent text, 1px accent border, `bg: var(--soft)`, radius 4).
    - Name (18px/600) + description paragraph (13.5px muted).
    - Footer (mono, faint, space-between): `meta` (e.g. `Abu Dhabi · 2019–24`) + accent `view profile ↗`.
      Below the grid: a divider + mono uppercase label `delivered for — international clients` + a wrapping row of **client chips** (`<a>` pills, radius 999): bold name + mono uppercase sector + accent `↗`.

8. **Stack (Skills)** `#stack` — H2 "The stack". Grid `repeat(auto-fit, minmax(250px,1fr))`, gap 44 of 3 skill groups. Each group: mono uppercase accent heading with a bottom border, then a column of items each rendered as grid `22px 1fr` with an accent `→` + label (16px). Below: a divider + mono `education` label + the degree line.

9. **Contact** (footer, top border, `bg-panel`) — mono `$ ./contact --now`, big H2 "Let's ship quality together.", then a row of three blocks (mono uppercase faint label + value): **email** (mailto link), **phone** (tel link), **based in** (Amman, Jordan). Bottom mono faint line `// © 2026 Khalid Saleh — built to pass`.

---

## 5. Theme toggle behavior

- On mount, read `localStorage['ks-bold-theme']`; if absent, use `window.matchMedia('(prefers-color-scheme: dark)')`.
- Toggle adds/removes `dark` on `<body>` and persists to localStorage.
- To avoid a flash of wrong theme (SSG), inline a tiny **blocking script** in `<head>` (or Next `beforeInteractive` script) that sets `document.body.classList` before paint. All theme-dependent UI (toggle label, accent lightening) must be `useEffect`-guarded to avoid hydration mismatch — render a stable default on the server.

---

## 6. Content data (`lib/data.ts`)

Reproduce verbatim. Types sketched; fill with the arrays below.

```ts
export const stats = [
  { value: "19+", label: "years in QA" },
  { value: "7",   label: "roles / suites passed" },
  { value: "4",   label: "QA teams led" },
  { value: "2",   label: "countries — JO · UAE" },
];

export type Role = {
  period: string; duration: string; title: string;
  company: string; location: string; about: string; points: string[];
};

export const roles: Role[] = [
  {
    period: "2019 — 2024", duration: "5 yrs", title: "Sr. Quality Testing / Team Lead",
    company: "Injazat Data Systems", location: "Abu Dhabi, UAE",
    about: "Injazat is one of the UAE’s leading digital-transformation and IT-services providers, running large-scale technology programs for government and enterprise clients. Here I led the QA function across parallel enterprise projects.",
    points: [
      "Led and managed the QA engineering team — allocation, mentoring, and delivery across concurrent enterprise programs.",
      "Developed and implemented end-to-end testing strategies aligned to requirements, timelines, and client expectations.",
      "Drove test-automation adoption (Selenium, JUnit, TestRail) and partnered with DevOps to build quality into CI/CD.",
      "Owned defect management: identification, triage, prioritization, and resolution tracking with developers.",
      "Reported quality metrics and release readiness to management and stakeholders.",
    ],
  },
  {
    period: "2014 — 2019", duration: "4.5 yrs", title: "Sr. Quality Testing",
    company: "C4 Advanced Solution", location: "Abu Dhabi, UAE",
    about: "C4 Advanced Solutions is an Abu Dhabi ICT company delivering mission-critical systems integration across the UAE. I set QA standards for new products spanning multiple projects.",
    points: [
      "Established and enforced QA measures and testing standards across the full development lifecycle.",
      "Designed test plans, cases, and designs against user stories and client requirements on multiple projects.",
      "Built and optimized QA automation and performance suites using Visual Studio.",
      "Influenced requirements and software design to maximize testability.",
    ],
  },
  {
    period: "2012 — 2014", duration: "1.5 yrs", title: "QA Manager",
    company: "MarkaVIP", location: "Amman, Jordan",
    about: "MarkaVIP was one of the Middle East’s largest flash-sale e-commerce platforms, serving shoppers region-wide at high transaction volume. I owned product quality strategy across the org.",
    points: [
      "Defined and deployed the product quality-assurance strategy across all phases of development.",
      "Supervised the QA lead, engineers, and testers — including evaluations and career development.",
      "Built QA metrics and quality checkpoints; anticipated release risks and escalated to protect delivery dates.",
      "Planned testing efforts and resource models with project managers for deployment and integration.",
    ],
  },
  {
    period: "2011 — 2012", duration: "1.5 yrs", title: "QA Coordinator — Gilt.com",
    company: "Aspire InfoTech", location: "Amman, Jordan",
    about: "Aspire InfoTech is an Amman outsourcing firm; here I coordinated offshore QA for Gilt Groupe, the New York luxury flash-sale retailer.",
    points: [
      "Coordinated regression, integration, and production-verification testing across offshore resources.",
      "Assessed QA effort per release and provided input on QA capacity planning.",
      "Built testing-analysis, progress, and reporting processes for accurate QA reporting.",
      "Managed QA resources through company policies and onshore communication.",
    ],
  },
  {
    period: "2010 — 2011", duration: "1.5 yrs", title: "QA Team Lead — Gilt.com",
    company: "Aspire InfoTech", location: "Amman, Jordan",
    about: "Main point of contact between Gilt’s New York team and the Amman delivery team on Agile releases.",
    points: [
      "Prepared and executed Selenium automation through Sauce Labs across browsers and operating systems.",
      "Determined integration-testing areas based on the impact of developed and affected modules.",
      "Defined project risks and contingency plans; ran daily follow-ups with the client contact.",
      "Owned all client deliverables — reports, analyses, and day-to-day communication.",
    ],
  },
  {
    period: "2008 — 2010", duration: "1.5 yrs", title: "Quality Team Lead — WorldNow",
    company: "Aspire InfoTech", location: "Amman, Jordan",
    about: "Led offshore QA for WorldNow, a New York company powering websites and video platforms for US local TV broadcasters.",
    points: [
      "Defined testing scope and developed manual and technical test plans for the team.",
      "Acted as the access point between the system-design team and the quality team.",
      "Managed site launches and production support; validated final deliverables of all phases.",
      "Produced daily/weekly client status reports and project sign-off documents.",
    ],
  },
  {
    period: "2005 — 2008", duration: "3 yrs", title: "QA Analyst → Sr. QA Analyst",
    company: "Aspire Services", location: "Amman, Jordan",
    about: "Delivered outsourced QA for Weight Watchers’ web platforms across the US, UK, Australia, and Canada; promoted to Quality Technical Supervisor.",
    points: [
      "Tested Weight Watchers releases: sign-up flows, database upgrades, Flex frameworks, and mobile web.",
      "Created test cases, test sets, and defect regression; reviewed activity diagrams.",
      "Reviewed defect reports and change requests before submission in Quality Center.",
      "Allocated testing tasks and prepared daily/weekly status reports as supervisor.",
    ],
  },
];

// Row index shown in UI as TC-0N (padStart 2). First row expanded by default.

export const companies = [
  {
    mono: "IDS", name: "Injazat Data Systems",
    sectors: ["Government-owned", "Enterprise IT"],
    desc: "Mubadala-owned national technology champion — digital transformation, cloud & cybersecurity for Abu Dhabi government and enterprise.",
    meta: "Abu Dhabi · 2019–24",
    url: "https://www.linkedin.com/company/injazat",
  },
  {
    mono: "C4", name: "C4 Advanced Solutions",
    sectors: ["Semi-government", "ICT & Defense"],
    desc: "ICT arm of Emirates Advanced Investment Group (EAIG) — systems integration, secure infrastructure and command-and-control solutions.",
    meta: "Abu Dhabi · 2014–19",
    url: "https://www.linkedin.com/company/c4-advanced-solutions",
  },
  {
    mono: "MV", name: "MarkaVIP",
    sectors: ["E-commerce", "Retail"],
    desc: "One of the Middle East’s largest flash-sale e-commerce platforms, serving shoppers region-wide at high transaction volume.",
    meta: "Amman · 2012–14",
    url: "https://www.linkedin.com/search/results/companies/?keywords=MarkaVIP",
  },
  {
    mono: "AI", name: "Aspire InfoTech / Services",
    sectors: ["IT Outsourcing"],
    desc: "Amman-based software & QA outsourcing house delivering testing for international clients across the US and Europe.",
    meta: "Amman · 2005–12",
    url: "https://www.linkedin.com/search/results/companies/?keywords=Aspire%20InfoTech",
  },
];

export const clients = [
  { name: "Gilt Groupe",     sector: "Luxury e-commerce · NY", url: "https://www.linkedin.com/company/gilt-groupe" },
  { name: "WorldNow",        sector: "Media / broadcast · NY", url: "https://www.linkedin.com/search/results/companies/?keywords=WorldNow" },
  { name: "Weight Watchers", sector: "Consumer health · Global", url: "https://www.linkedin.com/company/ww" },
];

export const marquee = [
  "Selenium","Sauce Labs","TestRail","JUnit","Quality Center",
  "Visual Studio","CI/CD","Agile","Automation","Regression","Defect triage","Test strategy",
];

export const skillGroups = [
  { name: "leadership", items: ["Team leadership","QA strategy","Resource planning","Mentoring","Client management","Risk management"] },
  { name: "testing",    items: ["Test planning","Test case design","Regression","Integration","Defect management","Agile & Waterfall"] },
  { name: "tools",      items: ["Selenium","Sauce Labs","TestRail","JUnit","Quality Center","Visual Studio","CI/CD"] },
];

export const profile = {
  name: "Khalid Saleh",
  role: "Software Quality Assurance leader",
  tagline: "Nineteen years turning ambiguous requirements into shipped, tested, trustworthy software — and QA teams that keep it that way.",
  email: "khalid-frihat@hotmail.com",
  phone: "+962 79 012 0141",
  location: "Amman, Jordan",
  cvHref: "/QA_Khalid_Saleh.pdf", // place the PDF in /public
};
```

---

## 7. Assets

- Put the CV PDF in `/public` (rename to `QA_Khalid_Saleh.pdf`, no spaces) and point the "download.cv" button at it.
- Photo: `/public/portrait.jpg` (or leave the `--soft` placeholder frame). The prototype used a drag-drop slot; in Next just use `<Image>` or a plain `<img>`.
- Company/client links open in a new tab (`target="_blank" rel="noopener noreferrer"`). Injazat and C4 are direct LinkedIn pages; MarkaVIP, Aspire, and WorldNow use LinkedIn search URLs (those firms have rebranded/merged). No company logos are used — **monogram tiles only** (trademark-safe).

---

## 8. Quality bar / acceptance

- Pixel-match the prototype at 1280px and mobile (375px) widths, both light and dark.
- All animations present: blinking cursor, seamless marquee loop (+ pause on hover), scroll-progress bar, suite expand `fadeUp` + chevron rotate, card hover lift.
- Fully responsive: hero grid collapses on small screens (hide photo <720px); all `auto-fit` grids reflow.
- No hydration warnings; theme persists across reloads; no FOUC.
- Semantic HTML, keyboard-focusable toggle and links, `aria-label` on the theme button. Add `prefers-reduced-motion` guards to pause the marquee/cursor for accessibility.
- Lighthouse: aim ≥95 across the board.

---

## 9. Suggested file tree

```
app/
  layout.tsx        # fonts, <body>, anti-FOUC theme script
  page.tsx          # composes all sections
  globals.css       # tokens + keyframes above
components/
  ScrollProgress.tsx
  StatusBar.tsx      # includes ThemeToggle
  Hero.tsx
  StatStrip.tsx
  Suites.tsx         # expandable rows, client component
  Marquee.tsx
  Companies.tsx
  Stack.tsx
  Contact.tsx
  ThemeProvider.tsx  # localStorage + matchMedia + accent tinting
lib/
  data.ts
public/
  QA_Khalid_Saleh.pdf
  portrait.jpg
```

Client components (`"use client"`): ScrollProgress, StatusBar/ThemeToggle, Suites, ThemeProvider. Everything else can be server components.
