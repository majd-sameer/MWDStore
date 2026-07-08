# Bid Requests — Filterable Data Table (Design Spec)

Build the **UI/layout** for a data table with a multi-filter toolbar, matching the *structure, spacing, and interactions* of the reference below.

> **Design-only brief.** This spec describes visuals and behavior — **not data**. Wire it to real data separately. Use placeholder/empty states where content would go.
>
> **Colors are NOT from the screenshot.** Ignore the coral/red/pink in the reference entirely. Every color, font, radius, shadow, and spacing value must come from the **existing project theme**. If a token is missing, add it to the theme file — never inline a raw hex in a component.

---

## 0. Ground rules

- **Detect the project stack first** (React/Vue/Svelte/Angular + Tailwind / CSS Modules / styled-components / MUI, etc.) and build in it. Do not introduce a new UI library if one already exists.
- **Token-driven styling only.** Reference `primary`, `surface`, `border`, `text`, `muted`, and semantic status tokens. No hex/rgb literals inside components.
- **Responsive:** table scrolls horizontally on narrow screens; the filter bar wraps gracefully.
- **Accessible:** semantic `<table>`, keyboard-navigable dropdowns, visible focus rings, `aria-label`s, correct roles on menus/checkboxes.
- **States to design:** default, hover, focus, active/selected, disabled, loading (skeleton rows), and **empty** ("No results").

---

## 1. Layout anatomy

```
┌───────────────────────────────────────────────────────────────┐
│  [Filter ▾] [Filter ▾] [Filter ▾] [Filter ▾]          [Clear]  │  ← toolbar
├───────────────────────────────────────────────────────────────┤
│  HEADER ROW (tinted band)                                      │  ← sticky optional
│  ─────────────────────────────────────────────────────────────│
│  row  ·  hover tint  ·  thin bottom border                     │
│  row                                                           │
│  …                                                             │
├───────────────────────────────────────────────────────────────┤
│  Total {n}                 Lines per page [15 ▾]   ‹ 1 2 … 4 5 ›│  ← footer
└───────────────────────────────────────────────────────────────┘
```

Vertical rhythm: toolbar → 16–24px gap → table → footer. Card container with theme `surface` background, `border` outline, and the theme's default radius/shadow.

---

## 2. Filter toolbar

Horizontal bar of multi-select dropdown filters, with a **Clear** action pinned to the far right.

**Filters (labels only — no fixed option data):**
`Bid Status` · `Request Type` · `Request Status` · `Time Left to Bid`

**Trigger button design:**
- Ghost/outline button using `border` + `text` tokens; chevron (`⌄`) on the right that rotates when open.
- When selections exist, show a small **count badge** + the word **"Selected"** → `Bid Status  [3]  Selected  ⌄`.
- Count badge fill = theme **primary**; badge text = primary-contrast. Nothing else in the bar uses primary.
- Empty state: just `Bid Status  ⌄`.
- Hover/focus/open states each visually distinct from theme tokens.

**Dropdown panel design:**
- Popover anchored to the trigger; `surface` bg, `border`, theme radius + elevation shadow.
- Vertical list of checkbox rows (checkbox + label); comfortable hit targets (min 36–40px height).
- Checked state uses primary; row hover uses a neutral/primary-tint token.
- Opens on click, closes on outside-click and `Esc`, traps focus while open.
- Optional slots to design if the project wants them: a search field at top, and "Select all / Clear" at the bottom.

**Clear button:**
- Text/ghost button at the far right, `muted` until hovered.
- Disabled/dimmed when no filters are active.

---

## 3. Table

**Columns (headers, left → right):**
`Request ID` · `Request Type` · `Policyholder` · `Vehicle Owner` · `Bid Amount` · `Bid Status` · `Request Status` · `Time Left to Bid` · `Action`

**Header row:**
- Subtle **tinted band** derived from theme primary (e.g. `primary/5` or a `--surface-accent` token) with `muted` header text, uppercase or medium weight per the project's type scale.
- Optional sticky header on scroll. Optional sort affordance (chevrons) if the project supports sorting.

**Body rows:**
- Comfortable cell padding; thin bottom border per row from `border`.
- Row **hover** = neutral or primary-tint background token.
- Numeric/currency cells left-aligned to match reference; keep alignment consistent per column.

**Cell rendering patterns to design (not the data itself):**
- **Status pill** — rounded chip with a leading dot; used for things like a `Pending`/`Open Bid`/`Closed Bid` state. Map to **semantic status tokens**:
  - active/attention (e.g. Pending, Open Bid) → `warning`/`info` token
  - neutral/closed (e.g. Closed Bid) → `muted`/`neutral` token
  - resolve exact mapping to whatever the project already uses for chips.
- **Avatar + label** cell — small circular avatar (with initials fallback) followed by an entity label; used where an owner/company appears. Design both this variant **and** a plain status-pill variant for the same column.
- **Empty value** — render `-` in `muted` for cells with no value (e.g. time left on a closed bid).
- **Action** — `View Details` as a link/ghost button in the theme **primary/link** color, with hover underline/emphasis.

**Skeleton (loading):** shimmer bars sized to each column. **Empty:** centered "No results match your filters" using `muted` text.

---

## 4. Pagination footer

Row beneath the table:

- **Left:** `Total {n}` in `muted` text.
- **Right:** `Lines per page` label + small select (design the closed + open states), then page controls `‹ 1 2 … 4 5 ›`.
- **Active page** = filled with theme **primary** + contrast text; **inactive pages** = neutral/ghost buttons; prev/next chevrons disabled-dim at range ends; ellipsis (`…`) non-interactive.

---

## 5. Theming checklist (do this, don't skip)

- [ ] Read the project's theme/token source before writing any styles.
- [ ] Header band, active page button, filter count badge, checked checkboxes, and action link all reference the project **primary** — whatever it is.
- [ ] Status pills reference existing **semantic status tokens**, not literal colors.
- [ ] No hex/rgb literals in components; add missing tokens to the theme file.
- [ ] All interactive elements have hover + focus-visible states from tokens.
- [ ] Verify in **light and dark** mode if the project supports dark mode.
- [ ] Confirm nothing coral/red leaks in from the reference.

---

## 6. Deliverables

- Table UI split into sensible subcomponents: `FilterToolbar`, `FilterDropdown`, `DataTable` (header/row/cells), `StatusPill`, `AvatarCell`, `Pagination`.
- Components accept content via **props** (columns + rows passed in) so data can be wired later — ship with placeholder/empty state, **no bundled dataset**.
- Any new theme tokens added, documented in the theme file.
- A usage example (or Storybook story) mounting the component with empty/skeleton/populated states.