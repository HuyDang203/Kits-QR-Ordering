# ANNAP — Production Enforcement System
*Operational law for the production frontend.*
*Every rule in this document is binding. No exception exists without written doctrine authority.*

**Governing authority:** `ANNAP_UNIFIED_IDENTITY_DOCTRINE.md`
**Supporting references:** `ANNAP_IMPLEMENTATION_DOCTRINE.md`, `ANNAP_EXPERIENCE_BLUEPRINT.md`, `ANNAP_PHASE0_UI_AUTHORITY.md`

---

## PREAMBLE

The doctrine defines *what* ANNAP is. This document defines *how* that identity is enforced in production code.

Doctrine without enforcement is aspiration. This document converts aspiration into constraint. Every section below functions as a production contract — a set of conditions that, if violated, mean the implementation is wrong regardless of how it looks in isolation.

This document answers the question every engineer and designer will eventually ask:

> *"Is what I'm building correct for ANNAP?"*

If it passes every rule in this document, yes. If it fails any rule, no — regardless of whether it looks good, feels intuitive, or ships faster.

---

## PART 1 — THE DESIGN ENFORCEMENT SYSTEM

### 1.1 Surface Hierarchy

The guest experience exists on exactly three visual planes. Every element in the production frontend belongs to one plane and only one. Elements on the wrong plane break the spatial logic of the entire experience.

#### Plane 0 — The Room

The persistent atmospheric ground. The world the guest inhabits.

| Allowed | Forbidden |
|---|---|
| Ambient background gradient (paper or depth) | Any animated color change triggered by user interaction |
| Static grain texture overlay | Glassmorphism applied to this layer |
| Very slow sinusoidal ambient drift | Transitions faster than `4000ms` |
| Mode atmosphere color temperature | Hard background-color changes on page load |

**Production implication:** `Plane 0` is always `position: fixed`, `z-index` at `--z-room` (0–10), and never receives `pointer-events`. It is not a component. It is never toggled or conditionally rendered. It is the room, and rooms do not disappear.

**Forbidden example:** Setting `background-color` on `body` to a flat hex color. The room is not a hex color. The room is a warm, living surface.

---

#### Plane 1 — The Letter

The correspondence content the guest reads and interacts with.

| Allowed | Forbidden |
|---|---|
| Paper-surface `--paper-warm` background | Floating card components (shadow + radius + contrast background) |
| Ink typography on warm paper | White or near-white surfaces on a contrasting dark background |
| Atmospheric drink photography | Product-shot imagery on plain backgrounds |
| Single action element per view | Multiple CTA elements competing at the same scroll position |
| Staggered element entrance (unfold timing) | Simultaneous content appearance |

**Production implication:** Every screen that presents content to the guest lives on Plane 1. This plane uses warm paper as its surface. It scrolls. It breathes. It has the editorial silence specified in the doctrine. `z-index` at `--z-letter` (30–50).

**Forbidden example:** A menu item displayed as a white card with drop shadow on a dark gray background. This is a floating card. Cards are banned. The menu item lives on the paper surface — it is not elevated above it.

---

#### Plane 2 — The Ceremony

Transient overlay moments. Events of emotional significance.

| Allowed | Forbidden |
|---|---|
| Tasting tray (invoked by gesture) | Persistent visible tray even before items are added |
| Confirmation bloom overlay | Alert dialogs or modal confirmation prompts |
| Mode atmosphere transition | Slide-in panel transitions |
| Drink detail expansion | Full-page navigation to a separate route |
| Arrival choreography overlay | Loading screens with progress bars |

**Production implication:** Plane 2 elements are temporary by definition. They arrive with ceremony, they serve their purpose, they dissolve. Nothing on Plane 2 persists longer than the interaction requires. `z-index` at `--z-ceremony` (60–100).

**Forbidden example:** A "cart" icon in the header that persists at all times. The tasting tray is Plane 2 — it exists only when invoked and when it has content. A persistent icon is Plane 1 UI chrome, which violates the One Action Rule and the Anti-App system.

---

### 1.2 Interaction Hierarchy

Every interactive element belongs to exactly one of three interaction tiers. The tier determines the element's visual weight, motion behavior, and surface treatment.

#### Tier 1 — Primary Invitation

One per screen. The single action the screen is built around.

- Visual treatment: Typeset phrase, full width, `--ease-settle` on tap
- Motion: Full ceremony sequence on activation (minimum `280ms` feedback)
- Spacing: Minimum `96px` above the element, `48px` below
- Copy: Hospitality vocabulary only. Never imperative commerce language.
- Examples: "Add to your tasting" / "Send to the bar" / "Begin your evening"

#### Tier 2 — Contextual Response

Zero to one per screen. Appears only after a primary action has been taken.

- Visual treatment: Smaller, lower opacity than Tier 1, appears via `--ease-unfold` `280ms` after the primary action resolves
- Motion: No ceremony — functional appearance only
- Spacing: Immediate proximity to the Tier 1 result
- Examples: "Continue exploring" / "See what else we suggest"

#### Tier 3 — Environmental Navigation

Zero to one per entire session. Hidden unless specifically sought.

- Visual treatment: Ghost register, accessible only via long-press or slow swipe gesture
- Motion: `Brief` duration only, no ceremony
- Spacing: No dedicated screen real estate
- Examples: Language selection (long-press wordmark), Service call (idle state tap menu)

**The absolute rule:** No screen may display more than one Tier 1 element. No screen may display a Tier 1 and Tier 3 element simultaneously. If a screen appears to need two Tier 1 elements, the screen is trying to do two things and must be split into two moments.

---

### 1.3 Typography Hierarchy

Applied to every text node in the production frontend. No text element exists outside this system.

| Register | CSS Class | Max per screen | Can coexist with |
|---|---|---|---|
| Display | `.annap-reg-display` | **1** | Narrative, Functional |
| Narrative | `.annap-reg-narrative` | 3 blocks | Display, Functional, Ghost |
| Functional | `.annap-reg-functional` | 4 instances | Any |
| Ghost | `.annap-reg-ghost` | 2 instances | Functional only |

**The one-hero rule enforced:** If a Razor partial or React component renders a Display element, it must assert `data-type-hero="true"` as a data attribute. A page-level validator (implementable as a CSS counter or JS assertion in development) should warn when more than one `data-type-hero` element is visible simultaneously in the viewport.

**Forbidden typography combinations:**

- Display text inside a card component (a hero inside a container breaks the spatial logic)
- Two Narrative blocks at identical font size, weight, and opacity competing for the same visual zone
- Functional text used for primary drink names (drink names are always Display or Title register — never Functional)
- Any text with `font-weight: 700` or higher — no bold text exists in ANNAP
- Underlines, strikethroughs, or other inline decorations on Narrative or Display text
- Text in colors outside the `--ink-*` palette

---

### 1.4 Motion Hierarchy

Motion in ANNAP has authority levels. Higher authority motion supersedes lower.

| Level | Name | Examples | Interruptible? |
|---|---|---|---|
| 1 | Ceremonial | Arrival bloom, confirmation bloom, mode commit | Never |
| 2 | Scene | Zone transitions, detail expansion, tray rise | Only by Level 1 |
| 3 | Element | Menu item reveal, stagger entrance, action feedback | Only by Levels 1–2 |
| 4 | Ambient | Background drift, grain, idle breathing | By anything |

**Enforcement rules:**

- A Level 1 motion, once begun, runs to completion. No user interaction interrupts it.
- During any Level 1 or Level 2 motion, all `pointer-events` on Plane 1 content are `none`. The guest cannot accidentally interact with content mid-ceremony.
- Level 3 elements that have not yet completed their entrance animation do not respond to interaction until `transform` and `opacity` have reached their final values.
- No two Level 2 motions run simultaneously. Queue them with the mandatory still moment between.

**Forbidden motion patterns:**

- Any animation with `animation-iteration-count: infinite` on a Plane 1 content element (looping animations on content destroy the stillness of the letter)
- CSS `:hover` transitions that move an element more than `4px` in any direction
- Any `@keyframes` declaration that changes `width`, `height`, `padding`, `margin`, or `top/left/right/bottom` — only `transform` and `opacity` and `clip-path`
- Any transition on `background-color` of a content surface (use overlay opacity transitions instead)
- `transition-duration: 0ms` on anything that has an emotional role — functional elements may use `0ms`, but emotional elements must have presence

---

### 1.5 Atmospheric Hierarchy

Atmosphere is a system, not a decoration. It follows strict output rules.

**Layer stack (precise z-index values):**

```
z-index: 0     → --annap-ambient-bg (radial gradient, the room warmth)
z-index: 5     → --annap-mode-layer (mode atmosphere color overlay, opacity 0–0.15)
z-index: 10    → --annap-grain (noise texture, opacity 0.035–0.055, fixed, never animated)
z-index: 20    → --annap-particle (adventurous mode canvas only, opacity 0.08–0.18)
z-index: 30    → --annap-letter-surface (the paper plane — all content)
z-index: 50    → --annap-ceremony-overlay (tray, detail expansion, confirmation)
z-index: 90    → --annap-arrival (arrival choreography, always on top until dismissed)
```

**Atmospheric rules:**

- The grain layer never animates. It is rendered once and persists.
- The ambient background gradient performs its sinusoidal drift via `requestAnimationFrame` updating `--gradient-cx` and `--gradient-cy` CSS custom properties. Full cycle: `45000ms`. This is atmosphere, not animation.
- The mode atmosphere layer transitions when a mode is committed, using a `600ms` `--ease-unfold` opacity transition. It never transitions again during the session.
- The particle canvas (adventurous mode only) renders at `devicePixelRatio: min(window.devicePixelRatio, 2)`. It is disabled entirely when `navigator.hardwareConcurrency <= 2` or `deviceMemory <= 2`.

---

### 1.6 Reading Rhythm Hierarchy

Every content screen has a reading rhythm. Reading rhythm governs the vertical sequence in which information arrives in the guest's field of view.

**The canonical reading rhythm for menu items:**

```
Position 1: Atmospheric image      — arrives first, full width, no text
Position 2: [silence — 16px]
Position 3: Display text (name)    — arrives 160ms after image
Position 4: [silence — 8px]
Position 5: Narrative text (note)  — arrives 300ms after image
Position 6: [silence — 4px]
Position 7: Functional text (meta) — arrives 480ms after image
Position 8: [atmospheric gap]      — 80px minimum before next item
```

**The canonical reading rhythm for the drink detail:**

```
Position 1: Atmospheric image      — 55% viewport height
Position 2: Display text (name)    — 300ms after expansion completes
Position 3: [silence — 24px]
Position 4: Evocative sensory line — 440ms after expansion
Position 5: [silence — 16px]
Position 6: Sommelier note         — 580ms after expansion
Position 7: [silence — 48px]
Position 8: Occasion context       — requires slight scroll to reach
Position 9: [silence — 64px]
Position 10: Action element        — earned by scrolling, never immediately visible
```

**Forbidden reading rhythm violations:**

- Action element visible without any scroll (the guest must dwell before they can act)
- Price appearing before the drink name (price is always subordinate to experience)
- Two Narrative blocks without any silence between them
- Functional text appearing before the Display text on any surface

---

### 1.7 Emotional Pacing Hierarchy

The guest experience moves through emotional states at a defined pace. Emotional pacing is the governing constraint on transition timing, content density, and screen complexity.

| Phase | Emotional State | Pacing Rule | Violation |
|---|---|---|---|
| Arrival (0–8s) | Reception | Nothing interactive until 3.2s have elapsed | Showing a menu immediately after QR scan |
| Mode selection | Authorship | One mode visible at a time, 8s dwell minimum | Showing all three modes simultaneously |
| Menu discovery | Exploration | Max 1.5 items visible per viewport | Showing 3+ items fully visible simultaneously |
| Drink detail | Intimacy | Action element never at top of view | "Add to tasting" visible without scrolling |
| Tray review | Authorship | Tray shows only after gesture, never auto-popups | Tray appearing automatically when item is added |
| Confirmation | Ceremony | `200ms` silence before bloom begins | Immediate visual feedback without the pause |
| Idle/waiting | Rest | `90s` of inactivity before table presence mode | Showing a timer or progress indicator |

---

## PART 2 — PRODUCTION COMPONENT RULES

### 2.1 Menu Items

**Maximum visual complexity:** 4 elements total — image, name, descriptor, meta line. No more.

**Allowed interactions:** 1 — tap to expand into drink detail.

**Forbidden on a menu item:**
- Quantity selectors
- "Add to cart" / "+" buttons directly on the menu item
- Star ratings or review counts
- "Popular" / "Recommended" / "New" badges
- Price in a colored bubble or pill
- Multiple images or image carousels
- Visible border separating the item from adjacent items (the atmospheric gap serves this purpose)

**Spacing expectations:**
- Internal padding: `32px` horizontal, `40px` top, `32px` bottom
- Between items: `80px` atmospheric gap (rendered as `padding-bottom` not `margin-bottom`)
- The image: full width (`calc(100% + 56px)` negative margin if within a padded container, bleed to screen edges)

**Motion expectations:**
- Entrance: `opacity 0→1` + `translateY(12px→0)` over `560ms --ease-unfold`
- Stagger: each child element staggered by `140ms`
- Tap feedback: `scale 1.0→0.98→1.0` over `200ms --ease-settle` on the image, not the container
- Exit (when detail expands): `opacity 1→0` over `300ms --ease-fold` for all non-selected items

**Emotional role:** A letter in a bundle of correspondence. Each one deserves individual attention. The menu item does not compete for the guest's attention — it waits to be noticed.

---

### 2.2 Drink Detail Surface

**Maximum visual complexity:** 6 elements — image, name, evocative line, sommelier note, occasion context, action element. One screen, one drink, nothing else.

**Allowed interactions:** 2 — scroll to explore, tap action element to add.

**Forbidden on the drink detail:**
- "Back" button (use swipe-down gesture only)
- Breadcrumb navigation
- "You might also like" recommendations inline
- Quantity selector (single add per detail visit; revisit the detail to add more)
- Persistent cart/tray indicator visible during detail reading
- Any element that references the menu overview while the detail is open

**Spacing expectations:**
- Image height: `55%` of viewport height, edge-to-edge
- Content area top padding: `32px` from image edge
- Horizontal content margin: `28px`
- Silence before action element: `64px`
- Action element bottom padding from screen bottom: `env(safe-area-inset-bottom) + 32px`

**Motion expectations:**
- Expansion: `clip-path: inset(y1 0 y2 0) → inset(0 0 0 0)` over `520ms --ease-unfold`
- Content entrance: staggered per reading rhythm (see 1.6)
- Action element: single gentle pulse on first enter-view (`scale 1.0→1.01→1.0, 800ms, once`)
- Collapse: `450ms --ease-fold`, exact reverse of expansion

**Emotional role:** A private conversation between the guest and one drink. The sommelier has left the table, and the guest is alone with the recommendation. The screen holds still. The guest reads at their own pace.

---

### 2.3 Mode Selection

**Maximum visual complexity:** 3 elements per mode — mode name (Display), atmospheric subline (Narrative), full-screen atmospheric surface.

**Allowed interactions:** 1 — tap anywhere to select this mode.

**Forbidden on mode selection:**
- All three modes visible simultaneously
- "Compare modes" functionality
- Radio buttons, checkboxes, or any form control
- Mode icons or category icons
- A "skip" or "back to home" option (this is the first act of authorship — it is not skippable)
- Any visible progress indicator ("Step 1 of 3")

**Spacing expectations:**
- Mode name: vertically centered at `45%` of viewport height (slightly above true center — more intimate)
- Subline: `24px` below the mode name
- No other elements in the layout

**Motion expectations:**
- Mode-to-mode transition: `600ms --ease-unfold` crossfade with simultaneous background temperature shift
- Mode selection commit: `900ms` ceremonial transition to menu discovery
- Auto-advance dwell time: `8000ms` per mode, looping, if guest does not interact

**Emotional role:** An invitation letter. The guest is being asked how they want to feel tonight. This is the most intimate question in the product — it must be asked with space, with stillness, with the confidence of someone who knows the answer matters.

---

### 2.4 Tasting Recommendations (Sommelier Layer)

The contextual sommelier guidance layer. Appears after `3000ms` of scroll inactivity on the menu, or when explicitly invoked.

**Maximum visual complexity:** 2 elements — a single Narrative line, a Ghost-register prompt.

**Allowed interactions:** 1 — tap to receive the full recommendation (activates sommelier mode which curates the scroll to a specific suggested drink).

**Forbidden:**
- Appearing as a persistent UI panel
- Showing multiple recommendations simultaneously
- Using the Functional register (this is conversational, not informational)
- Auto-advancing to the recommendation without guest acceptance
- Appearing more than once per session without guest re-invocation

**Spacing expectations:**
- Appears anchored to the bottom `20%` of the viewport, above the ambient tray indicator
- Horizontal margin: `28px`
- Self-dismisses after `4000ms` if not interacted with

**Motion expectations:**
- Entrance: `opacity 0→0.85, translateY(8px→0)` over `480ms --ease-unfold`
- Dismiss (timeout): `opacity 0.85→0` over `600ms --ease-fold`
- Dismiss (tap): accepted tap triggers the recommendation journey; the line dissolves immediately

**Emotional role:** The sommelier leaning slightly toward the table and murmuring a suggestion. Not a recommendation engine. Not a featured product. A quiet, trusted, personal voice.

---

### 2.5 Tasting Tray

The composition surface — what the guest has chosen for this evening.

**Maximum visual complexity:** Header, item list, total line, single action. Nothing else.

**Allowed interactions:** 3 — scroll within the tray, left-swipe to remove item, tap "Send to the bar."

**Forbidden on the tasting tray:**
- Promo code field
- Upsell / "add more" suggestions
- Multiple payment options
- Delivery address, contact info, or any personal data collection
- Order notes field visible by default
- Item thumbnails (list is typeset, not visual)
- Visible close button (close by swipe-down gesture only)

**Spacing expectations:**
- Tray height when open: `78%` of viewport height
- Item row height: `56px` minimum (generous — never cramped)
- Between items: `8px`
- Header to first item: `32px`
- Last item to total line: `24px`
- Total line to action: `40px`
- Action bottom padding: `env(safe-area-inset-bottom) + 24px`

**Motion expectations:**
- Rise: `translateY(100%→0)` over `520ms --ease-settle`
- Dismiss: `translateY(0→100%)` over `400ms --ease-fold`
- `backdrop-filter: blur(32px)` applied only after rise animation completes (via `transitionend`)
- Item remove: `height collapse` over `240ms` after swipe-confirm, adjacent items slide up to fill
- Item add (while tray is open): new item fades in at list bottom over `320ms`

**Emotional role:** The composed tasting menu, presented before service begins. The guest sees what they have chosen, expressed as a beautifully typeset list. They experience their own taste as a coherent evening rather than a shopping cart.

---

### 2.6 Confirmation Moment

The most ceremonial moment in the guest journey.

**Maximum visual complexity:** 3 elements — wordmark, confirmation copy, table identifier.

**Allowed interactions:** 0 during the ceremony. After ceremony completes: the idle table presence tap menu.

**Forbidden:**
- Order number displayed as primary information
- Receipt-style itemized list
- "Track your order" mechanism
- Timer or wait indicator with numerical countdown
- "Rate your experience" prompt
- Any action requiring the guest to do anything

**Spacing expectations:**
- Wordmark: absolute center of viewport
- Confirmation copy: `28px` below wordmark
- Table identifier: `48px` below confirmation copy, Ghost register, smallest legible size

**Motion expectations:**
- Pre-bloom silence: `200ms` of complete stillness after "Send to the bar" tap
- Bloom: `clip-path: circle(0% at tapX tapY) → circle(150% at tapX tapY)` over `1200ms --ease-arrive`
- Tray dissolves within bloom: `opacity 1→0` over `600ms` beginning at `0ms`
- Confirmation elements entrance: staggered beginning at `800ms` — wordmark first, copy second, table identifier third
- Table presence transition: `1800ms` fade into idle ambient state after `90s` of inactivity

**Emotional role:** The sealed acknowledgement. The order has been received with grace. The guest is now a guest — no longer a user. The experience is complete and the product retreats into warmth, waiting to be needed again.

---

### 2.7 Overlays

Any surface that appears above the letter plane.

**Maximum visual complexity:** Depends on overlay type — see component rules above. Never more than the minimum required for the purpose.

**Rules:**
- Never more than one overlay visible simultaneously
- All overlays are dismissed by gesture, never by a close button (exception: if accessibility requires a visible dismiss target, it must be in Ghost register, minimum size `44px`, positioned outside the primary content zone)
- Overlays that require backdrop-filter receive it only after their entrance animation completes
- All overlays use the Ceremony plane (`z-index: 60–100`)

**Forbidden overlay types:**
- Alert dialogs with OK/Cancel buttons
- "Are you sure?" confirmation dialogs (trust the guest's decision)
- Feature announcement overlays
- Permission request prompts embedded in the experience flow
- Cookie consent banners appearing during the experience
- Bottom sheets that feel like app drawers (use the tray pattern only)

---

### 2.8 Transitions Between Scenes

Scene transitions are not navigation. They are the movement between movements in a piece of music.

**The transition taxonomy:**

| Transition | Type | Duration | Behavior |
|---|---|---|---|
| Arrival → Mode | Bloom dissolve | `900ms` | Arrival fades into mode atmosphere |
| Mode → Menu | Compression + rise | `1100ms` | Mode compresses upward, menu rises |
| Menu → Detail | Expansion | `520ms` | Item expands to fill, others dissolve |
| Detail → Menu | Compression | `450ms` | Reverse of expansion, 15% faster |
| Any → Confirmation | Bloom | `1200ms` | Warm bloom from tap point covers everything |
| Confirmation → Idle | Drift | `1800ms` | Imperceptible fade into ambient presence |

**Transition rules:**
- The mandatory still moment (`120ms–400ms`) before every transition, not during
- `pointer-events: none` on all Plane 1 elements during any Level 2 motion
- Background atmosphere (`Plane 0`) never transitions — it persists across all scene changes
- URL updates via `history.pushState` occur at the midpoint of the transition, never at the start or end

---

### 2.9 Imagery

All images in the production experience are governed by the following contract.

**The image contract:**

1. **Subject:** The drink in an atmospheric context. Never a white-background studio shot.
2. **Grading:** Warm color treatment applied — reduce blue channel `10–15%`, add amber overlay at `8%` opacity using `mix-blend-mode: multiply`.
3. **Vignette:** Radial gradient overlay at `z-index: 1` within the image container — transparent at center, `rgba(28, 20, 10, 0.4)` at outer 20%.
4. **Loading:** Blur-up placeholder — a 20px wide base64 JPEG blurred to `20px` via CSS, cross-fading to the full image on load over `400ms`.
5. **Format:** WebP primary, JPEG fallback. Minimum two `srcset` breakpoints: `400w` and `800w`.
6. **Aspect ratio:** `16:9` for menu items, `16:10` for drink detail (slightly taller), `3:2` for mode atmosphere.
7. **alt text:** Descriptive of the drink's sensory character, not its physical description. "A moment of stillness with cold floral jasmine" not "Photo of jasmine cold brew in a glass."

**Forbidden image treatments:**
- `object-fit: contain` with visible padding/letterboxing
- Images with visible drop shadows (the vignette provides all necessary edge treatment)
- Images that animate on scroll independently of their container
- Transparent PNG images on paper background (always atmospheric JPEG/WebP)
- Images displayed at their actual pixel size without atmospheric treatment

---

### 2.10 Spacing Cadence

Spacing is the grammar of the guest experience. The following values are the only acceptable spacing values for the letter content plane.

```
4px    — minimum internal micro-spacing (within a single text element grouping)
8px    — between closely related elements (name → subline of same drink)
16px   — between distinct elements within the same section
24px   — breath — the pause between a section's content and its boundary
28px   — horizontal content margin (left and right, always)
32px   — section padding (top/bottom of content within a surface)
48px   — chapter gap (between major content zones on a screen)
64px   — silence (before/after a typographic hero, before an action element)
80px   — atmospheric gap (between menu items, between letters)
96px   — ceremony space (arrival screen, confirmation screen)
120px  — full editorial pause (used once, before the most important element on a screen)
```

**The forbidden spacing values:** Any pixel value not in this list, applied to the letter content plane. Tailwind arbitrary values (`px-[22px]`, `mt-[50px]`) are prohibited in content components. Only named tokens.

**The golden spacing test:** Look at any screen and remove the spacing mentally. If the content feels the same, there is not enough spacing. Spacing in ANNAP is never decorative — it is structural. It is the silence between sentences.

---

### 2.11 Scroll Pacing

The guest should feel that scrolling through ANNAP is a qualitatively different experience from scrolling through any other interface. This requires active engineering.

**Scroll behavior contract:**
- Menu scroll container: `overflow-y: scroll; -webkit-overflow-scrolling: touch; scroll-snap-type: none`
- No scroll-snap (scroll-snap forces navigation metaphor, ANNAP scroll is reading metaphor)
- Scroll within the detail content area: separate inner scroll container, not body scroll
- Body scroll disabled while any Plane 2 overlay is open

**Parallax implementation:**
- Ambient background elements move at `8–10%` differential rate relative to content scroll
- Maximum parallax offset: `12%`
- Implementation: Intersection Observer tracking scroll position, updating CSS custom property `--scroll-parallax-offset`, applied via `transform: translateY(calc(var(--scroll-parallax-offset) * 0.08))`
- Never applied to text elements — only to atmospheric image elements and the ambient background

**Velocity-responsive behavior:**
- On high scroll velocity (detected via `touchmove` delta > `20px/frame`): pause all non-essential animations (ambient drift, element micro-interactions)
- Resume ambient animations `500ms` after scroll velocity drops below threshold

---

## PART 3 — THE ONE ACTION RULE

### Definition

At any moment in the guest experience, exactly one meaningful action is available to the guest. Not one prominent action and several minor ones. Not one primary CTA and several secondary links. One action. Singular.

This is not a visual design principle. It is a **hospitality principle**. A great host does not present a guest with a menu, a wine list, a dessert card, a cocktail menu, and the bill simultaneously. They present one thing at a time, in the right sequence, with the confidence that the guest can handle one thing fully before the next arrives.

### How This Applies in Production

**Menu browsing state:**
- Available action: Tap a drink to enter its detail.
- Nothing else is interactive. No cart icon. No language toggle. No filter tabs.
- The ambient tray indicator (a thin warm line at the bottom) is visible after the first item is added, but it is not interactive in this state — it is environmental, not actionable.

**Drink detail state:**
- Available action: "Add to your tasting" (requires scroll to reach — see Reading Rhythm rules).
- Collapse gesture (swipe down) is a navigation gesture, not an action — it does not count toward the one-action budget.
- No other buttons, links, or interactive elements are present.

**Tasting tray state:**
- Available action: "Send to the bar."
- Remove-item swipe gesture is contextual to each row, not a screen-level action — it does not count.
- Dismiss gesture (swipe down) is navigation, not action.

**Mode selection state:**
- Available action: Tap anywhere on the current mode to select it.
- No "skip" action. No "go back" action. No "compare" action.

**Confirmation state:**
- Available action: None during the ceremony. After completion, the idle tap menu offers two contextual options. Never both visible simultaneously.

### Competing CTA Groups — Explicitly Banned

A competing CTA group is any UI pattern where two or more interactive elements with distinct outcomes are visible simultaneously at comparable visual weight.

**Banned examples:**

```
[Add to cart]  [Save for later]     ← Two actions of equal weight
[Order now]  [View full menu]       ← Two actions of equal weight
[Solo]  [Group]  [Adventurous]      ← Three modes visible simultaneously (banned)
[Send order]  [Keep browsing]       ← Two actions at the tray bottom
```

**The enforcement mechanism:** Every component that renders an interactive element must declare its `data-action-tier` attribute (`tier-1`, `tier-2`, or `tier-3`). A development-mode validator counts visible `tier-1` elements in the viewport. If the count exceeds 1, a console warning fires.

### Multi-Action Clusters — Explicitly Banned

A multi-action cluster is a group of controls that each perform distinct functions and that appear together as a visual unit.

**Banned examples:**
- A header row with: [ANNAP logo] [language toggle] [cart icon] [mode indicator]
- A menu item with: [image] [name] [price] [+ add button] [bookmark icon]
- A tray row with: [item name] [quantity] [-] [+] [delete] [price]

**The rule:** Any UI element that exists primarily to give the guest control over the interface (rather than to present content or invite a single response) is a multi-action cluster element and does not belong in the letter experience.

### Dashboard Controls — Explicitly Banned

Dashboard controls are interactive elements designed for repeated adjustment and management. They have no place in a correspondence experience.

- Filters: Banned. Mood categories are invoked via long-press, not visible tabs.
- Sort controls: Banned entirely. The sommelier curates the sequence — the guest does not re-sort it.
- Search: Banned from the primary experience. If it must exist (for very large menus), it lives behind the same long-press as the category layer.
- Quantity steppers on menu items: Banned. Quantity is managed in the tray.
- Toggle switches: Banned.
- Checkboxes: Banned.
- Segmented controls: Banned.

### Simultaneous Emphasis — Explicitly Banned

Simultaneous emphasis means applying equal or near-equal visual weight to multiple elements on the same screen, creating a hierarchy where nothing leads and everything competes.

**Detection:** If a screen has more than one element with `font-weight > 300` in the Display or Title register, it has simultaneous emphasis. If a screen has more than one colored element (using the `--wax-*` palette), it has simultaneous emphasis. Either condition is a violation.

---

## PART 4 — THE MOBILE QR LAW

### Jurisdiction

This section governs all behavior when the ANNAP guest experience is accessed via QR code on a mobile device. This is the primary use case. Every decision optimizes for this context first.

**Canonical device:** iPhone 14 (390px width, 844px height), Safari 17, portrait orientation, QR scanned via native Camera app.

**Secondary devices:** iPhone SE (375px), Android mid-range (360px+), in-app browsers (LINE, Instagram).

**Non-target contexts:** Landscape orientation (unsupported), desktop (unsupported for guest experience), tablet (unsupported).

### Viewport Behavior

```css
/* Required on html and body */
html, body {
  height: 100%;
  min-height: -webkit-fill-available;
  overflow: hidden; /* Body does not scroll — content containers scroll */
}

/* All full-screen surfaces use dvh, never vh */
.full-screen-surface {
  height: 100dvh;
  /* Fallback for older Safari: */
  height: calc(var(--vh, 1vh) * 100);
}

/* JS sets --vh on load and resize */
/* document.documentElement.style.setProperty('--vh', `${window.visualViewport.height * 0.01}px`); */
```

**Safe area enforcement:**
```css
.letter-content-area {
  padding-top: max(env(safe-area-inset-top), 16px);
  padding-bottom: max(env(safe-area-inset-bottom), 24px);
}

/* The ambient background bleeds into safe areas — never clips to safe area */
.annap-ambient-bg {
  position: fixed;
  inset: 0; /* Not inset: env(safe-area-inset-top) */
}
```

**Address bar behavior:**
- The arrival choreography begins only after `window.onload`, not `DOMContentLoaded`
- `window.visualViewport` resize events must update `--vh` to handle Safari address bar hide/show
- The arrival wordmark vertical position uses `--vh`-based centering, not `50vh`

### Thumb Interaction Zones

The human thumb reaches comfortably into specific screen zones when holding a phone one-handed in portrait. ANNAP engineering must respect this topology.

```
Full thumb reach (right thumb):
  Bottom 60% of screen, left half of screen

Comfortable stretch:
  Bottom 70% of screen, full width

Difficult reach (requires grip shift):
  Top 30% of screen

Never-without-two-hands:
  Top 15% of screen, far corners
```

**Enforcement rules derived from this:**
- The primary action element (Tier 1) must always be positioned in the bottom `50%` of the viewport
- The tray indicator (swipe-up zone) lives at the bottom `48px` — the easiest thumb reach
- The ANNAP wordmark in ambient state lives at the top center — intentionally unreachable except when the guest shifts grip (this prevents accidental language toggle)
- No interactive element above `30%` from the top of the screen during normal browsing

### Reading Cadence on Mobile

Reading on a handheld device is fundamentally different from reading on a desktop. The line length, font size, and content density must reflect this.

**Mobile reading law:**
- Maximum line length: `30ch` for Narrative text (`max-width: 30ch` — never removed)
- Minimum font size for Narrative: `15px` (never smaller — mobile reading is imprecise)
- Minimum tap target size: `44px × 44px` for any interactive element, even if the visual element appears smaller
- Extended tap target via `padding` or `::before` pseudo-elements — the visual and interactive areas may differ
- Touch feedback: every interactive element must respond to `touchstart` within `16ms` (`touch-action: manipulation` removes the 300ms delay)

### Image Pacing on Mobile

Images on mobile scroll off screen as quickly as they arrive. The pacing must compensate.

**Image pacing law:**
- Minimum image height for menu items: `55vw` (portrait-safe minimum atmospheric presence)
- Maximum image height for menu items: `65vw` (prevents single image overwhelming the scroll)
- The atmospheric gap below each image (before the drink name): `16px` minimum, never zero
- Images must be fully rendered before the text elements stagger in — use the `load` event to trigger stagger, not a fixed `setTimeout`
- Blur-up placeholder height must match the final image height exactly — no layout shift on load

### Scene Transitions on Mobile

**The mobile transition budget:**
- At any time, maximum one scene transition in progress
- All transitions use `transform` and `opacity` only — no layout-recalculating properties
- `will-change: transform` is declared in advance on the three primary transition candidates: arrival overlay, tasting tray, drink detail panel
- `will-change` is removed after each transition completes

**The in-app browser rule:**
Transitions in LINE and Instagram WebViews have degraded performance. Detect via `navigator.userAgent` and apply the reduced-motion profile if the agent suggests an in-app context where confirmed degradation has been observed.

### Motion Density on Mobile

The total amount of simultaneous motion on mobile must be actively limited.

**Motion density budget (mid-range device):**
- Maximum 3 properties animating simultaneously across the entire document
- `opacity` and `transform` on separate elements count as separate animation budget items
- `backdrop-filter` is applied only to resting surfaces, never to animating ones — costs the full budget on its own during application
- The ambient background drift (sinusoidal via `requestAnimationFrame`) counts as 1 budget item permanently

**Visual silence on mobile:**
Visual silence means intentional non-content — areas of the screen that carry no information and require no attention. On mobile, silence is structural. Without it, the experience feels like a responsive website.

Every screen must have minimum `30%` of its vertical content height occupied by silence (atmospheric background only, no text, no image foreground). This is not waste. This is breathing room. It is what makes the screen feel like a letter rather than a list.

---

## PART 5 — THE LETTER RULES

### How Paper Behaves

Paper in ANNAP is a physical metaphor that governs every content surface. When in doubt, ask: how would this behave if it were real paper?

**Paper rules:**

1. **Paper has corners.** All content surfaces use `border-radius: 0`. Rounded corners signal a digital component, not a physical material.

2. **Paper has weight.** When a surface rises or appears, it moves with the settled authority of something that has mass. The `--ease-settle` curve is mandatory for any surface arriving from off-screen.

3. **Paper is flat.** Content on a paper surface does not hover above it. There are no floating card elements within a paper surface — only content embedded in the material.

4. **Paper holds light.** The paper surface is never pure white — it carries the warmth of the ambient light in the room. The texture overlay gives it fiber. The vignette gives it depth.

5. **Paper does not pulse or breathe.** Once paper has arrived, it is still. The ambient atmosphere breathes. The grain is present. But the paper itself is a resting surface.

6. **Paper can be stacked.** The tasting tray rises above the paper surface — it is a second sheet, elevated. But nothing rises above the tray except the confirmation ceremony. Stacking is sparse.

### How Surfaces Stack

The exact stacking model for every context:

**During menu discovery:**
- Plane 0 (ambient background) → Plane 1 (menu paper surface with items)
- Two planes only. No overlay, no tray (until items exist).

**After first item added:**
- Plane 0 → Plane 1 → Ambient tray indicator (thin warm line, `2px`, purely atmospheric)
- Three visual elements. The indicator is not a surface — it is a mark.

**During drink detail:**
- Plane 0 → Plane 1 (menu dimmed) → Plane 2 (detail surface, elevated paper)
- The menu does not disappear — it remains, dimmed, visible behind the detail. This communicates depth.

**With tray open:**
- Plane 0 → Plane 1 (menu/detail, accessible behind tray) → Plane 2 (tasting tray)
- The content beneath the tray is visible through the frosted surface. This communicates that the world still exists while the tray is being reviewed.

**Confirmation:**
- Everything dissolves into the bloom. One plane: the ceremony.

### How Content Reveals

Content in ANNAP does not appear. It is revealed.

The distinction: appearing is sudden, binary (hidden then visible). Revealing is gradual, earned (the guest's gaze is prepared before the content arrives).

**The reveal contract:**

Every element in the letter experience uses the following approach:

1. Element enters the DOM in its initial state: `opacity: 0`, `transform: translateY(12px)`
2. When the Intersection Observer fires (element crossing into viewport with `rootMargin: '0px 0px -60px 0px'`), the reveal class is applied
3. The reveal class transitions to: `opacity: 1`, `transform: translateY(0)`, over `560ms --ease-unfold`
4. Each child element within the item staggers by `140ms`

**What never reveals:**
- Background/atmospheric elements (they are simply present from page load)
- The grain texture (present, never animated)
- The ambient wordmark in idle state (it fades in during the confirmation-to-idle transition, not via scroll reveal)

### How Correspondence Is Paced

The pacing of the correspondence is the pacing of a thoughtful letter being read aloud. Not fast, not slow — measured. Each sentence allowed to land before the next begins.

**The pacing law:**

Between every major piece of information on a screen, there is a visual pause. Not a loading pause — a **designed pause**. The pause is silence space (see Spacing Cadence, section 2.10).

The sommelier note in the drink detail is not a block of text. It is a series of sentences with a visible breathing rhythm between them. If the sommelier note is longer than 3 sentences, it is broken into two Narrative blocks with `24px` between them — giving the guest the sensation of turning a page within the note.

### How Notes Appear

Sommelier notes, mode invitations, and all hospitality copy in ANNAP follow the voice of a handwritten note.

**Voice rules for all copy:**

1. **One idea per sentence.** No compound sentences joined with "and" or "but" unless the compound carries deliberate poetic weight.
2. **No bullet points.** Lists are e-commerce. Correspondence is prose.
3. **No bold emphasis within body copy.** If a phrase deserves emphasis, restructure the sentence to place it at the beginning or end — typographic positions of natural weight.
4. **No parenthetical asides.** The sommelier does not hedge. They recommend with confidence.
5. **No question marks in descriptions.** "What will you taste?" is a prompt. ANNAP doesn't prompt — it guides. "The first thing you'll notice is the jasmine." Not "Wondering what to expect?"
6. **Present tense for descriptions.** "The cold brew opens with a clarity that lingers." Not "The cold brew will open..."

### How Recommendations Are Framed

A recommendation in ANNAP is not a featured product. It is a confidence — the sommelier's quiet certainty that this is right for this guest at this moment.

**Recommendation copy structure:**
1. The moment or mood — first. ("For the afternoon that calls for quiet.")
2. The sensory promise — second. ("Something cold, something floral, something that doesn't ask anything of you.")
3. The drink name — third. Last. The reveal after the setup.

**Forbidden recommendation frames:**
- "Most popular this week:" — social proof, not hospitality
- "You might also like:" — algorithmic, not curated
- "Featured item:" — promotional, not personal
- "Staff pick:" — institutional, not intimate

### How Confirmations Are Acknowledged

The confirmation is not a receipt. It is not a summary. It is an acknowledgement — the host's gracious receipt of the guest's order.

**The confirmation voice law:**

The confirmation copy is written in the register of gratitude and assurance. It confirms:
1. That the order was received (warmly, not mechanically)
2. That the guest need do nothing further
3. That the experience is now in capable hands

**Forbidden confirmation content:**
- Order ID or reference number as primary displayed information
- Itemized list of ordered items (the guest knows what they ordered — they don't need to see a receipt)
- Estimated wait time as a countdown or numerical display
- "Rate your experience" prompt at this moment
- Social sharing invitation
- "While you wait" upsell content

---

## PART 6 — THE ANTI-APP SYSTEM

Every pattern listed in this section immediately breaks the immersion of the ANNAP correspondence experience. Each is accompanied by the precise reason it creates the wrong signal.

### Floating Cards

**What it is:** A content container with visible border, border-radius > `4px`, background color contrasting with its parent surface, and a drop shadow creating the illusion of elevation.

**Why it breaks ANNAP:** A floating card communicates "this is a discrete digital component." The guest's brain immediately recognizes it as a UI element — something to be interacted with, managed, dismissed. Cards signal SaaS. They signal dashboard. They signal e-commerce. The letter experience disappears the moment a card appears because the guest has left the paper world and entered the interface world.

**The signal it sends:** "You are using software."

**What to do instead:** Embed content directly in the paper surface. Use the atmospheric gap and silence spacing to create visual separation between items. The separation is spatial, not containerized.

---

### Segmented App Sections

**What it is:** A horizontal tab row, a segmented control, or a pill-group that divides content into named categories displayed simultaneously.

**Why it breaks ANNAP:** Segmented sections force the guest to manage information architecture. They present the question: "Which of these should I be in right now?" This is the opposite of hospitality. The sommelier does not say "Please select: Cold Brew / Espresso / Tea." The sommelier guides.

**The signal it sends:** "You are managing a database."

**What to do instead:** Present the menu as a curated sequence. Categories are an invoked layer, hidden by default, accessed only when the guest actively seeks a specific type.

---

### Sticky Tab Rails

**What it is:** A row of category tabs that sticks to the top (or bottom) of the screen as the user scrolls.

**Why it breaks ANNAP:** Sticky rails are the visual signature of food delivery apps. They exist because those products need the guest to navigate a large, uncurated inventory. ANNAP's menu is not an inventory. It is a curated tasting. The rail also consumes permanent vertical space — space that belongs to the letter, to the silence, to the atmospheric breathing.

**The signal it sends:** "This is a food delivery app."

---

### App Navbars

**What it is:** A persistent horizontal bar at the top or bottom of the screen containing: logo, navigation items, utility icons (cart, search, profile, language).

**Why it breaks ANNAP:** Navbars are the structural skeleton of an app. Their presence signals to the guest that they are in a container — that there are multiple sections to navigate between, multiple tools available at all times. ANNAP has one journey, one direction, one present moment. There is nothing to navigate to from any screen that the experience does not naturally reveal at the correct time.

**The signal it sends:** "You are inside an app. Manage it."

---

### Bright CTA Buttons

**What it is:** A button with a saturated fill color (`#FF5500`, `#00A3FF`, any vivid tone) that demands visual attention.

**Why it breaks ANNAP:** Bright buttons are designed to compete for attention. They exist to interrupt the guest's reading and redirect them to an action. In the ANNAP correspondence, the guest is reading a letter. An interruption — a bright rectangle demanding a tap — is the equivalent of a banner ad appearing in the middle of a handwritten note.

**The signal it sends:** "Buy now. Act now. Don't read — convert."

**What to do instead:** The action element is a typeset phrase. It does not compete for attention. It waits. It is found, not imposed.

---

### Colored Chips / Pill Tags

**What it is:** Small, pill-shaped elements with rounded corners (`border-radius: full`) and a background fill color, used to categorize, label, or filter content.

**Why it breaks ANNAP:** Pills are the visual language of social media, SaaS products, and modern UI frameworks. They communicate: "This is a tag. Tags are how we organize digital content." The letter experience has no tags. Functional information is typeset in the Ghost or Functional register directly in the content flow — not extracted into a separate visual element.

**The signal it sends:** "This is a digital interface with a taxonomy system."

---

### Dense Controls

**What it is:** Multiple interactive elements with distinct functions clustered in close proximity — quantity steppers, filter dropdowns, sort controls, toggle switches.

**Why it breaks ANNAP:** Dense controls require the guest to context-switch from experiencing to operating. The interface becomes a tool to manage rather than a space to inhabit. Dense controls are the antithesis of the hosted experience.

**The signal it sends:** "You are in control. Configure this."

---

### Utility Spacing

**What it is:** The default spacing assumptions of most UI frameworks — `16px` horizontal margins, `8px` between elements, `12px` padding inside components — designed for information density rather than atmosphere.

**Why it breaks ANNAP:** Utility spacing optimizes for fitting more content in less space. ANNAP optimizes for the guest's emotional experience of each piece of content. Cramped spacing communicates urgency and density. The letter experience communicates leisure and care.

**The signal it sends:** "We have a lot of products to show you. Scroll faster."

---

### Grid-Heavy Layouts

**What it is:** A two-column or three-column grid of equally-sized content items (like a product gallery or image grid).

**Why it breaks ANNAP:** A grid presents multiple items as equivalent — "here are six things, evaluate them." The correspondence presents each item as singular and worthy of individual attention. A grid is browsing. The correspondence is receiving.

**The signal it sends:** "Browse our selection."

---

### Toast Notifications

**What it is:** A small notification banner that appears briefly at the top or bottom of the screen to confirm an action ("Item added to cart", "Order placed successfully").

**Why it breaks ANNAP:** Toasts are interruptions. They arrive unexpectedly, demand a moment of attention, and then disappear. They are borrowed from operating systems and communication apps — contexts where interruption is appropriate. In the correspondence experience, the action already has its ceremony (the "Add to your tasting" ritual, the confirmation bloom). A toast notification reduces that ceremony to a notification. It tells the guest: "I processed your request." The ceremony tells the guest: "I received your choice with care."

**The signal it sends:** "Your request has been processed. System acknowledged."

---

### Modal Spam

**What it is:** Dialogs that interrupt the experience to ask for confirmation, collect information, or present choices. Common examples: "Are you sure you want to remove this?", "Please select your table number", "Accept our terms before continuing".

**Why it breaks ANNAP:** Modals are the hospitality equivalent of the waiter stopping the meal to ask if you want to be on the email list. They break the flow of the experience entirely and force the guest into a transactional interaction they did not initiate.

**The signal it sends:** "We need something from you before we continue."

---

### Visible Cart Counts

**What it is:** A badge or number overlay on a cart icon showing how many items the guest has selected.

**Why it breaks ANNAP:** Cart counts are merchant thinking in guest's clothing. They remind the guest at every moment that they are in a purchasing flow — that the system is counting their selections. The tasting tray indicator is warm, ambient, present. It does not count. It does not number. It simply indicates that a composition is in progress.

**The signal it sends:** "You have added N items. Continue shopping."

---

### Bottom App Docks

**What it is:** A persistent row at the bottom of the screen with navigation icons: Home, Menu, Cart, Profile.

**Why it breaks ANNAP:** Bottom docks are the structural signature of mobile apps. Their presence signals, unmistakably, "you are in an app with multiple sections." ANNAP has one journey. There is no "Home" to return to. There is no "Profile" to manage. The dock is architecture for a product that ANNAP is not.

**The signal it sends:** "Choose where you want to go."

---

## PART 7 — THE ANNAP TEST

Every screen, component, or interaction produced for the ANNAP guest experience must pass all seven of the following tests before it enters production. Passing six out of seven is not acceptable. All seven are required.

---

### Test 1 — The Letter Test

**Question:** If this screen were printed on warm cream paper, would it look like a private tasting letter from a thoughtful sommelier?

**Pass criteria:**
- The primary typographic element is at the scale and weight of a letter's opening line
- The content reads as prose, not as a list or spec sheet
- The spacing is generous — the letter has margins, silence, breathing room
- The overall impression is personal and curated, not systematic and comprehensive

**Fail signals:**
- The screen looks like a menu PDF printed from a restaurant management system
- Multiple sections compete for the eye's first attention
- The content feels like it was written to be scanned, not read

---

### Test 2 — The Hospitality Test

**Question:** If the world's best sommelier were delivering this experience in person, would they behave this way?

**Pass criteria:**
- Only one thing is being offered at this moment
- The pacing feels considered, never rushed
- No action is demanded before the guest has had a moment to settle
- The language is warm, assured, and never salesman-like

**Fail signals:**
- The screen asks the guest to do multiple things simultaneously
- Copy uses urgency language ("limited availability", "order now")
- The experience requires the guest to manage information rather than receive it

---

### Test 3 — The Silence Test

**Question:** Is there enough silence on this screen?

**Silence is defined as:** Screen area occupied by the ambient paper surface only — no text, no image foreground, no interactive elements.

**Pass criteria:**
- Minimum `30%` of the screen's vertical content height is silence
- Before the primary action element: minimum `64px` silence
- Before the Display-register typographic hero: minimum `96px` silence

**Fail signals:**
- Every part of the screen contains information
- The content begins at the very top of the scroll container
- The action element is visible without any scroll

---

### Test 4 — The Editorial Test

**Question:** Would this screen appear without embarrassment in a luxury publication about coffee culture and contemplative hospitality?

**Pass criteria:**
- The typography is restrained, refined, and purposeful
- The image treatment is atmospheric, not commercial
- The layout has the confidence to leave significant space empty
- The color palette feels warm, considered, and never trendy

**Fail signals:**
- The screen looks like it was designed using default Tailwind classes
- The image looks like a stock photo or product shot
- The layout would fit comfortably in any generic food-ordering app

---

### Test 5 — The Tactile Test

**Question:** Does the interaction feel physical — like handling paper, receiving something, or making a quiet gesture?

**Pass criteria:**
- The motion timing makes the interaction feel like it has weight
- Tap feedback is present but not performative
- The primary action feels like placing something rather than clicking something
- Transitions feel like turning pages or unfolding paper, not like app navigation

**Fail signals:**
- The interaction feels identical to any other mobile app
- Transitions are snappy, bounce, or use spring physics with obvious overshoot
- The addition of a drink to the tray produces no ceremony — just a number incrementing

---

### Test 6 — The One-Action Test

**Question:** At this moment, is there exactly one meaningful action available to the guest?

**Pass criteria:**
- Only one interactive element in Tier 1 is visible in the viewport
- Navigation gestures (swipe-down to return) do not count as actions for this test
- The single available action is the most natural next step from the current emotional state

**Fail signals:**
- Two or more interactive elements of comparable visual weight are visible simultaneously
- The guest has to choose between paths rather than follow the single prepared one
- Any dashboard-style controls are visible

---

### Test 7 — The iPhone Table Test

**Question:** If a guest is sitting at a café table, holding this iPhone portrait in one hand, with warm ambient light, having just scanned a QR code — would this feel like receiving a private correspondence?

**Pass criteria:**
- The experience works perfectly one-handed
- The most important interactive element is in the thumb's comfortable reach zone
- The font size is readable at arm's length without squinting
- The screen looks beautiful resting face-up on the table
- The guest's first emotional response is "this is different" — not "this is an app"

**Fail signals:**
- Anything requires two hands to interact with
- The experience looks like a mobile website
- The screen is information-dense enough that the guest feels they must manage it
- The overall impression in this physical context is: "ordering system"

---

## PART 8 — IMPLEMENTATION PRIORITIES

The following ranking governs the order in which all remaining guest-facing production work is approached. No `MEDIUM` task begins before all `CRITICAL` tasks are complete. No `LOW` task begins before all `HIGH` tasks are verified.

### CRITICAL — Immersion-Breaking, Guest-Facing, Highest Visibility

These must be addressed before any other implementation work proceeds. Their absence or incorrect state means the ANNAP identity is not in production regardless of everything else.

| Priority | Task | Why Critical |
|---|---|---|
| C-01 | **Arrival choreography** — QR scan → warm bloom → wordmark → welcome line. No interaction for 3.2s minimum. | The guest's first 8 seconds define the entire experience. If this is wrong, nothing else can compensate. |
| C-02 | **Paper surface establishment** — Replace all dark-mode UI shells, white cards, and gray backgrounds in the guest flow with the paper/ink token system. | The visual register is wrong until paper is the default surface. |
| C-03 | **Remove all persistent navigation chrome** — Eliminate sticky headers, bottom navbars, persistent cart icons, tab rails from the guest experience. | These signal "app" more powerfully than any other single element. |
| C-04 | **Typography enforcement** — Apply the four-register system. Ensure Display register has exactly one hero per screen. Remove all `font-weight: 700+`. | Typography is the voice of the letter. Wrong typography means the wrong letter. |
| C-05 | **Eliminate all toast notifications** — Replace cart addition toast with the Add Ceremony (radial pulse + label crossfade + ghost travel). | The add-to-tray moment is the highest-emotion micro-interaction. Toast notification is the most anti-ANNAP possible response to it. |

---

### HIGH — Significant Immersion Impact, Core Journey Moments

| Priority | Task | Why High |
|---|---|---|
| H-01 | **Mode selection rebuild** — Full-screen sequential mode presentation. One mode at a time. Auto-advance with 8s dwell. | The mode selection is the guest's first act of authorship. Currently it likely feels like a filter screen. |
| H-02 | **Menu item editorial layout** — Single column, edge-to-edge atmospheric images, 80px atmospheric gaps, staggered element reveals. | Menu is the primary exploration space. If it feels like a product list, discovery never becomes experience. |
| H-03 | **Drink detail expansion** — Clip-path expansion from item position. Staggered content entrance per reading rhythm. Action element at bottom of scroll, never immediately visible. | The detail is the canonical screen — the emotional center of the product. |
| H-04 | **Tasting tray ceremony** — Gesture-invoked only. Rise with `--ease-settle`. Backdrop-filter after animation completes. No cart icon, no badge counter. | The tray is the guest's authorship moment. Transactional tray behavior destroys the composition metaphor. |
| H-05 | **Confirmation bloom** — 200ms silence → warm bloom → wordmark return → hospitality copy → idle transition. No receipt. No order number as primary element. | Confirmation is the emotional peak of the transaction. It must be ceremony, not receipt. |
| H-06 | **Spacing audit and enforcement** — Apply doctrine spacing values across all guest screens. No utility spacing values. Minimum `28px` horizontal margins. | Cramped spacing is the fastest way for a screen to feel like a generic app. |
| H-07 | **All copy rewrite** — Replace every instance of "Add to Cart", "Order Now", "View Details", and all e-commerce vocabulary with the hospitality vocabulary from the doctrine. | Language is the voice of the correspondence. Wrong language breaks the letter immediately. |

---

### MEDIUM — Quality and Consistency, Emotional Refinement

| Priority | Task | Why Medium |
|---|---|---|
| M-01 | **Ambient background system** — Sinusoidal gradient drift via `requestAnimationFrame`. Mode atmosphere overlays. Grain texture at correct opacity. | The atmospheric layer is the room. Without it, content floats on a flat color. |
| M-02 | **Image treatment pipeline** — Warm color grading, edge vignette, blur-up placeholder, WebP with JPEG fallback, atmospheric alt text. | Images are currently either untreated product shots or atmospheric but inconsistent. Consistency is critical. |
| M-03 | **Sommelier layer** — Contextual guidance appearing after 3s scroll inactivity. Mode-aware single recommendation. Invoked, not persistent. | The sommelier voice is ANNAP's differentiator. Currently this guidance is likely embedded statically in UI. |
| M-04 | **Reading rhythm enforcement** — Stagger timing applied to all menu items and detail content. `rootMargin: '0px 0px -60px 0px'` Intersection Observer for all reveals. | Reveals feel abrupt or missing currently. Reading rhythm is what makes scrolling feel like letter-reading. |
| M-05 | **Motion audit** — Remove all easing values outside the four permitted values. Remove `animation-iteration-count: infinite` from content elements. Remove height/width transitions. | Motion inconsistency is detectable subliminally. It makes the experience feel unresolved even if the guest cannot name why. |
| M-06 | **Scroll performance** — Velocity-responsive animation pausing. Intersection Observer debouncing. Virtual scroll if menu > 20 items. Parallax capped at 12% offset. | Menu scroll is the primary interaction duration. Poor performance destroys immersion faster than any visual error. |

---

### LOW — Polish, Edge Cases, Enhancement

| Priority | Task | Why Low (but important) |
|---|---|---|
| L-01 | **Table presence / idle ambient state** — `90s` post-confirmation transition to ambient display. Periodic atmospheric copy lines. Tap to reveal contextual menu. | Emotionally significant but only experienced after the primary journey is complete. |
| L-02 | **Mode atmosphere persistence** — Session storage of mode atmosphere variables. Full-session color temperature shift. Mode-specific animation tempo multipliers. | Enhances the mode commitment payoff but requires the mode system itself to be correct first. |
| L-03 | **Adventurous mode particle system** — Canvas particle layer. Performance detection. Disable on low-end devices. | The most complex Adventurous mode element. Mode must be fully implemented first. |
| L-04 | **Language selection in arrival** — Device language detection. Before-choreography-completion language selection. SessionStorage persistence. Long-press wordmark for later change. | Important for Vietnamese guests but doesn't affect the visual identity. |
| L-05 | **prefers-reduced-motion compliance** — All animations behind `data-motion="full"` check. Instant state changes when reduced-motion is preferred. | Accessibility requirement. Cannot ship without this, but depends on the motion system being fully implemented first. |
| L-06 | **Performance optimization** — JS bundle splitting (arrival-critical / menu-critical / ambient). Image prefetch on scroll proximity. `touch-action: manipulation` audit. | Critical for production but depends on all features being implemented correctly first. |

---

## ENFORCEMENT APPENDIX — QUICK REFERENCE

### The Five-Second Check

Before any component is considered ready for production, look at it for five seconds and answer only this:

*"Does this look like a letter or does this look like an app?"*

If the answer is anything other than immediate, unambiguous "letter" — the component is not ready.

### The Token Compliance Checklist

Every production component must satisfy:

- [ ] No hex color values outside `tokens.css` definitions
- [ ] No `font-weight` values above `500` or below `200`
- [ ] No `border-radius` above `8px` on letter-plane content (exceptions: tray at `0px` with `backdrop-filter`)
- [ ] No easing values outside `--ease-unfold`, `--ease-fold`, `--ease-settle`, `--ease-drift`
- [ ] No `animation-duration` values outside the doctrine duration set
- [ ] Horizontal margin minimum `28px` on all content containers
- [ ] No `background-color: #FFFFFF` or pure white anywhere
- [ ] No `font-family` outside Playfair Display, EB Garamond, and the functional system font stack

### The Sentence That Must Be Impossible to Say

The final enforcement test for any production state:

If a guest could reasonably say any of the following after scanning the QR code, the implementation has failed:

- "Oh, it's one of those QR menu apps."
- "It's a pretty design but it's hard to find stuff."
- "It's a lot to take in."
- "It looks like an app."
- "Where's the cart?"
- "Can I just see all the drinks?"

If the implementation is correct, the guest will be unable to form any of these sentences. They will not have the vocabulary for them, because the experience will not have given them the context to think in those terms. They will simply be in it.

---

*ANNAP Production Enforcement System v1.0*
*Governing authority: ANNAP_UNIFIED_IDENTITY_DOCTRINE.md*
*This document supersedes all prior implementation standards not explicitly referenced herein.*
*No part of this document may be overridden by convention, convenience, or time pressure.*
