# ANNAP — Unified Experience Identity Doctrine
*The single authoritative document for all visual, interaction, and emotional decisions.*
*Version 1.0 — Locked. No implementation begins without this document as its governing authority.*

---

## PREAMBLE — THE IDENTITY PROBLEM THIS DOCUMENT SOLVES

The ANNAP guest system has drifted into multiple simultaneous visual registers: parts feel like a SaaS product, parts feel atmospheric, parts feel editorial, parts feel like a dark-mode dashboard. This fragmentation is not a styling inconsistency. It is an **identity crisis**. When a guest scans the QR code, they should enter one world — singular, complete, unmistakable. Right now they are entering several partial worlds at once.

This document ends that drift. Every decision going forward — every color value, every spacing unit, every animation curve, every word of copy — must derive from the single identity declared here.

---

## PART I — THE PRIMARY EXPERIENCE IDENTITY

### What ANNAP Is

ANNAP is a **hosted tasting correspondence**.

The guest has not opened an app. They have not scanned a menu. They have received a letter — a private, handwritten tasting note — delivered to their table by an unseen sommelier who knows exactly who they are and what this moment deserves.

Everything in the product must flow from this single metaphor. Not as decoration. As **structure**.

### What ANNAP Is Not

| NOT this | Because |
|---|---|
| A coffee ordering app | Apps are used. Letters are received. |
| A food delivery UI | Delivery is logistics. This is hospitality. |
| A digital menu | Menus are browsed. Correspondence is unfolded. |
| A dashboard | Dashboards monitor. This space inhabits. |
| A mobile website | Websites are visited. This is entered. |
| A luxury hotel website | Hotels are grand. This is intimate. |
| An ecommerce product | Products are selected. Tastings are discovered. |

### The Governing Metaphor — In Full

The guest is sitting at a café table. On the table, they find a small envelope — cream-colored, slightly textured, sealed with wax. Inside: a handwritten note. The handwriting is unhurried, elegant. The paper carries warmth from the room. The note reads as a personal recommendation — not a price list, not a specification sheet — a **letter from someone who cares what they taste tonight**.

Every screen, every transition, every line of copy, every spacing decision, every animation in ANNAP is a single answer to the question:

> *"Does this feel like that letter?"*

If the answer is no — it does not belong in ANNAP.

---

## PART II — THE PRIMARY CANONICAL SCREEN

### Definition

The canonical screen for the entire ANNAP experience is the **moment a guest opens the first tasting letter**.

This is not the arrival screen. It is not the menu. It is the **drink detail view** — the screen where a guest encounters a single drink in full. This screen is the emotional center of the entire product. Every other screen exists to lead toward it or to resolve away from it. Design every other screen in relation to this one.

### The Canonical Screen — Exact Description

**Surface:** Warm, slightly textured paper — cream to warm ivory, not white. The texture is imperceptible at arm's length, felt as warmth rather than seen as grain.

**Atmosphere:** A single, centered composition. The screen feels like a page in a private journal. The drink occupies the upper half as a deep, warm photograph — not a product shot, but an atmospheric image where the drink exists within a context: candlelight, a wooden surface, a moment.

**Typography — the ruling hierarchy:**
- One typographic hero: the drink's evocative name in display weight — large, thin-stroked, ink on paper. Nothing competes with it.
- Below: two or three lines of sommelier voice, in a slightly warmer weight, narrower column, generous leading. Written as a letter. Read as a letter.
- At the very bottom of the scroll: a single action — typeset, not button-shaped. A phrase in functional ink, restrained and confident.

**Spacing:** More than feels comfortable. Then more again. The white (cream) space is not emptiness — it is the paper breathing. It is the pause between sentences in a letter you read slowly.

**Motion:** Nothing moves on this screen except the guest's reading. The photograph is still. The typography is still. The only motion is the gentle ambient presence of the atmospheric background layer — imperceptible unless you look for it, like heat-shimmer over a warm surface.

**Emotional energy:** Private. Intimate. Unhurried. The guest should feel that this screen was composed for them specifically — that someone took care arranging it.

**Pacing:** Slow. The screen does not offer its action immediately. The guest must dwell. The action reveals itself at the bottom of the scroll — not hidden, but earned.

---

## PART III — THE MASTER VISUAL RULES

### 1. Borders

- **Permitted:** A single fine rule line (`1px`, ink at 10–18% opacity) used as a horizontal divider between major sections. This line is not decorative — it is a fold line in paper.
- **Permitted:** The outer edge of a letter surface — a subtle border that implies the physical edge of a page or envelope.
- **Banned:** Border-radius-heavy rounded borders that suggest app cards.
- **Banned:** Colored borders used as UI status indicators (success/error color borders).
- **Banned:** Multiple visible borders on a single component.

### 2. Radius

```
0px   — used on letter surfaces (paper has corners)
2px   — used on the smallest functional elements only (e.g. wax indicator dots)
4px   — maximum radius for any inline functional element
8px   — permitted only on overlaid surfaces (tasting tray) that must feel lifted
```

The dominant radius in ANNAP is **zero**. Paper has corners. Envelopes have corners. Stationery has corners. Round corners signal an app. ANNAP is not an app.

### 3. Shadows

Shadows in ANNAP simulate **paper depth** — the slight lift of a letter resting on a surface, the shadow a sealed envelope casts on the table.

```
No shadow     — for flat content embedded in the paper surface
Paper lift    — box-shadow: 0 2px 8px rgba(60, 40, 20, 0.12)
               Used for letter surfaces resting above the base
Sheet rise    — box-shadow: 0 8px 32px rgba(60, 40, 20, 0.18)
               Used for the tasting tray
Wax glow      — box-shadow: 0 0 24px rgba(140, 60, 30, 0.15)
               Used only on the wax seal confirmation element
```

**Banned:** Drop shadows with sharp edges. Colored shadows in app-UI colors. Multiple-layer drop shadows intended to create depth illusion.

### 4. Layering

The screen has three planes, in this exact order from back to front:

1. **The Surface** — the paper/ambient ground. Warm, still, persistent. This is the physical world the guest inhabits.
2. **The Letter** — the content plane. Pages, notes, tasting descriptions. This is the correspondence itself.
3. **The Ceremony** — the transient overlay plane. Tasting tray, confirmation bloom, transitions. These are the moments between letters.

Nothing should feel like it exists outside these three planes. No elements floating without context. No panels unanchored from the surface.

### 5. Spacing Rhythm

ANNAP's spacing is **editorial, not utilitarian**.

```
Base unit: 8px

Breath:       24px  — between related elements within a section
Section:      48px  — between distinct sections within a screen
Chapter:      80px  — between major emotional zones on a screen
Silence:     120px  — before and after a typographic hero moment
              Used sparingly — this is a full editorial pause
```

**The single most important spacing rule:** Every screen should contain **one more silence unit** than the designer thinks is necessary. If you believe the spacing looks right, add 20% more. Luxury is generous. Hospitality is never rushed.

Horizontal content margins: `28px` on mobile. Never the standard `16px`. Never the cramped `12px`. The extra space communicates ease and confidence.

### 6. Atmospheric Depth

The background of every screen is not a color — it is a **surface**. It has texture. It has temperature. It has a light source.

The primary surface is warm paper — a very slightly off-white or cream tone, with the faintest suggestion of fiber texture (rendered via a fixed, low-opacity noise overlay). As screens move deeper into the experience (arrival → mode → menu → detail), the surface temperature warms slightly — as if moving deeper into a candlelit room.

The guest should never be looking at a flat color background. They should feel like they are looking at a physical material.

### 7. Image Treatment

All images in ANNAP receive the following treatment without exception:

- Warm color grading — slight reduction in blue channel, slight increase in amber/red
- Edge vignette — radial gradient overlay darkening the outer 20% of the image
- No hard crop edges — images bleed or are masked with a soft atmospheric fade at their boundaries
- Never used as decoration — every image in ANNAP communicates the **atmosphere of the drink**, not its physical appearance

**Banned image treatments:**
- White-border product photography
- Studio-lit flat-lay food photography aesthetics
- Images with visible background removal (transparent PNG product shots)
- Brightly lit, colorful images with no atmospheric treatment

### 8. Paper Texture

A single noise texture tile (200×200px PNG, 8-bit grayscale) sits at `position: fixed, pointer-events: none, z-index: 10` over the entire viewport at all times.

Opacity: `0.035` in bright contexts, `0.055` in deeper contexts.

This texture is **never animated**. It is simply present. It is what makes the screen feel like material rather than glass.

### 9. Typography Hierarchy

The typographic system has four and only four registers. Every text element in ANNAP belongs to one of these:

**Display** — The single typographic hero of a screen.
- Typeface: Playfair Display, weight 300 italic or EB Garamond, weight 300
- Size: `clamp(36px, 8vw, 52px)`
- Tracking: `+0.04em`
- Color: `var(--ink-deep)` — full opacity
- Line height: `1.15`
- Usage: Drink names, mode invitations, the ANNAP wordmark, arrival welcome

**Narrative** — The voice of the sommelier.
- Typeface: EB Garamond, weight 400 or 400 italic
- Size: `16px`
- Tracking: `+0.01em`
- Color: `var(--ink-body)` — 85% opacity
- Line height: `1.75`
- Max width: `30ch` — intimacy constraint. Never allow lines longer than 30 characters on mobile.
- Usage: Drink descriptions, sommelier notes, occasion context, all hospitality copy

**Functional** — The signal layer.
- Typeface: Same family, weight 500 or 600
- Size: `11px`
- Tracking: `+0.12em`
- Text transform: uppercase
- Color: `var(--ink-faint)` — 50% opacity
- Usage: Prices, timestamps, action labels, categories

**Ghost** — The ambient presence.
- Same as Functional but at `0.25` opacity
- Usage: Table numbers, idle-state atmospheric text, environmental markers

**The supreme rule of typography:** Every screen has **exactly one** Display element. If a screen appears to need two Display elements, one of them is wrong.

### 10. Button Treatment

There are no buttons in ANNAP. There are **actions**.

Actions are typeset phrases that invite gesture, not rectangular containers that demand click. They are never filled with color. They are never rounded rectangles. They are never labeled with imperative verbs borrowed from e-commerce.

An action in ANNAP looks like this:

```
Add to your tasting                    →
```

A line of Functional-register text followed by a thin arrow. No border. No background. No hover fill. On interaction, the arrow extends slightly (4px, 200ms) before the ceremony begins.

The sole exception is the "Send to the bar" action at the bottom of the tasting tray — this may have a very thin, warm-toned rule above it to separate it from the item list. The rule is `1px`. Not a button border.

### 11. Transitions

All transitions in ANNAP move like **unfolding paper**. Slow into the open, settle with intention.

Four and only four easing values exist in the codebase:

```css
--ease-unfold:   cubic-bezier(0.16, 1, 0.3, 1);     /* Elements arriving */
--ease-fold:     cubic-bezier(0.7, 0, 0.84, 0);      /* Elements departing */
--ease-settle:   cubic-bezier(0.34, 1.56, 0.64, 1);  /* Physical placement */
--ease-drift:    linear;                               /* Ambient, atmospheric */
```

No other easing values. No `ease-in-out`. No `bounce`. No spring functions not explicitly listed here.

### 12. Motion Timing

```
Micro:       120ms  — selection state, tap feedback
Brief:       280ms  — contextual element show/hide
Standard:    480ms  — panel and letter transitions
Deliberate:  680ms  — scene zone transitions
Ceremonial:  900ms  — arrival, mode selection, confirmation bloom
Ambient:     4000ms+ — breathing cycles, atmospheric drift
```

---

## PART IV — BANNED UI LANGUAGE

The following elements are **permanently banned** from the ANNAP guest experience. Not conditionally. Not "unless it fits." Permanently.

### Structural Bans
- Floating card components (content in rounded-radius shadow boxes above a contrasting background)
- Segmented app sections (tabbed interfaces, segmented controls, toggle groups)
- Persistent bottom navigation bars
- Persistent top navigation bars
- Sticky header bars with logo + icons
- Side drawer navigation
- Hamburger menus
- Tab bars of any kind
- Pagination controls (numbered or prev/next)
- "Load more" buttons
- Infinite scroll pagination indicators
- Pull-to-refresh visual affordances

### Visual Element Bans
- Bright accent-color CTA buttons (any button with a saturated fill color)
- Pill-shaped chips (for categories, tags, filters, moods)
- Star rating displays
- Numerical review counts
- "Popular" badges, "Best Seller" ribbons, "New" labels
- Promotional banners or announcements
- Sale indicators, discount callouts
- Progress bars used as order status indicators
- Loading spinners of any kind
- Skeleton loading states (pulsing gray placeholder shapes)
- Toast notifications for cart/tray actions
- Alert dialogs that interrupt the experience
- Modal sheets with standard close-X buttons
- Floating action buttons (FAB)
- Colored dot indicators on navigation items (the e-commerce "you have N items" counter)
- Glassmorphism used decoratively or on multiple elements simultaneously

### Language and Copy Bans
- "Add to Cart"
- "Order Now" / "Place Order"
- "View Details"
- "Buy Now"
- "Check Out"
- "Cart" (standalone)
- "Total" (standalone, as a label)
- "Subtotal"
- "Item added" (as a toast message)
- "Loading..." (as any visible text)
- "Please wait"
- "Back to Menu"
- Any copy written in imperative salesman register

### Motion and Animation Bans
- Bounce animations of any kind
- Spring animations with overshoot greater than `1.56` in the `ease-settle` curve (already defined above)
- Rapid-fire sequential animations that feel playful or gamified
- Entrance animations with slide-from-left-to-right (lateral slide used in app navigation)
- Confetti, particle burst, celebration animations
- Any animation that calls attention to itself rather than serving the emotional moment
- Pulsing/blinking loading indicators
- Aggressive hover states that transform scale by more than `1.02`
- Simultaneous animation of more than 3 properties on a single element

---

## PART V — THE INTERACTION PHILOSOPHY

### The Core Principle

**The guest does not use ANNAP. ANNAP is offered to the guest.**

Every interaction must embody this inversion. In most digital products, the user performs actions: taps, scrolls, inputs, submits. In ANNAP, the guest receives invitations, unfolds correspondence, pauses in consideration, and makes quiet gestures of acceptance.

This is not a UX metaphor. It is a behavioral contract. It changes every interaction decision.

### The Five Interaction Modes

**1. Receiving** — The guest passively receives. Content arrives. Screens open. Correspondence unfolds. The guest does nothing except be present. This is the dominant mode for arrival and major transitions.

**2. Dwelling** — The guest pauses. They are reading. Considering. The interface holds still and does not interrupt. No auto-advance. No timeout pressure. No contextual prompts for the first three seconds of any new content state.

**3. Exploring** — The guest moves through the tasting letters. Scrolling is the primary gesture. Scrolling is not navigating — it is *reading further into the correspondence*. This distinction must govern every scroll design decision.

**4. Choosing** — The guest makes a quiet decision. A single tap. Not a confirmation dialog, not a multi-step flow. The decision, once made, is received with ceremony.

**5. Resting** — The guest has completed their composition. The screen becomes ambient. The correspondence has been sent. The guest rests in the warmth of having been well-hosted.

### Gesture Vocabulary

| Gesture | Meaning in ANNAP |
|---|---|
| Tap | Acceptance, interest, gentle invitation to go deeper |
| Long press | Access to hidden configuration (language, help) — never primary discovery |
| Scroll down | Reading further, moving through the tasting |
| Swipe up (from bottom) | Invoke the tasting tray |
| Swipe left (on tray item) | Gently remove a selection |
| Swipe down (on detail) | Return to the tasting overview |
| Double tap | Not used |
| Pinch/zoom | Disabled entirely |

### The Single Most Important Interaction Rule

**At any moment, the guest should only be able to do one thing.**

Not one menu item and also a cart icon and also a language toggle and also a category filter. One thing. Whatever the primary intention of the current moment is — that and nothing else is available. All other interactive affordances either do not exist on this screen or are invisible until contextually needed.

---

## PART VI — THE SCROLL PHILOSOPHY

### Scrolling as Letter Reading

Scrolling in ANNAP is **not navigation**. The guest is not moving through an interface. They are reading further into correspondence that was prepared for them. This distinction demands that:

- Content does not arrive at once. Each element enters the reading field individually, at a measured pace.
- The guest's scroll speed determines the reveal speed — but only above a minimum threshold. Content never jumps in faster than the guest can absorb it.
- There is no "bottom" in the usual sense. The guest reaches the end of a letter, and the letter ends gracefully — not with a "load more" button, but with a closing gesture, the way a real letter ends.

### Atmospheric Gaps

Between major content units (between individual drinks in the menu scroll), there is a deliberate atmospheric gap. This gap is not empty space. It is the space between letters — the moment where the previous content settles before the next arrives.

Specification:
- Height: `80px` minimum between drink entries
- Background: continuation of the ambient paper surface — the gap is the table between letters
- No divider line in this space — a line would imply structure, not pause

### Silence Spacing

Before every typographic hero element on any screen, there is a silence space of at minimum `96px`. This is non-negotiable. This silence is what allows a single Display element to command its full weight. Without the silence, hierarchy collapses. With it, the guest's eye knows exactly where to rest.

### Visual Breathing Rhythm

The cadence of elements within any single screen should follow this rough ratio:

```
Image / atmospheric moment     → 55–65% of screen height
Typography (hero + narrative)  → 25–30% of screen height
Silence / atmospheric gap      → 10–20% of screen height (distributed)
```

No screen should be more than 70% text. No screen should be more than 75% image. The balance between image and text is the balance between showing and telling — both are necessary, neither dominates.

### Image-to-Text Cadence

In the menu scroll, the rhythm is: **image arrives first, always.** The atmospheric photograph establishes the mood of the drink before a word appears. Text follows, staggered:

1. Image — full presence, atmospheric treatment
2. `160ms` pause — the photograph settles
3. Drink name (Display) — rises from 8px below, opacity 0→1
4. `140ms` pause
5. Sommelier descriptor (Narrative) — fades in
6. `180ms` pause
7. Occasion tag and price (Functional) — appears, understated

### Reveal Timing

Elements reveal as they enter the viewport, using an Intersection Observer with `rootMargin: '0px 0px -60px 0px'`. They should feel like they are rising to greet the guest, not jumping into view when the guest arrives.

The reveal animation per element: `opacity: 0 → 1` and `transform: translateY(12px) → translateY(0)` over `560ms` using `--ease-unfold`. No element reveals faster than this. Elements may reveal slower (for ceremonial moments) but never faster.

---

## PART VII — THE MOBILE QR EXPERIENCE

### The Context

A guest is sitting at a table in a café. They are holding their phone (iPhone, portrait) in one hand. The other hand holds a coffee, or rests on the table. The ambient light is warm — perhaps late afternoon, perhaps evening. They have just scanned a QR code.

They expect to see a menu. They should instead feel like they are receiving a letter.

### The Governing Constraint

This experience exists for and only for:
- Portrait orientation (landscape is not supported — the experience is not adapted, it is portrait-native)
- iPhone Safari and in-app browsers (LINE, Instagram)
- One-handed use
- A person at rest, not in transit

Every design decision must honor this context. Tap targets must be reachable with the right thumb without shifting grip. Content must not require two-handed interaction. The experience must work beautifully even if the guest's other hand is occupied.

### What the Experience Must Resemble

At every moment from QR scan to confirmation, the guest's screen should resemble **one of the following**:

- A handwritten note on cream paper
- An open tasting journal at a private table
- A sommelier's personal recommendation card
- A page from a beautifully produced editorial on wine and solitude
- The first page of a letter you did not know was coming

It must **never** resemble:

- A responsive website
- A product catalog on mobile
- A food delivery app
- A form flow
- A dashboard viewed on a small screen

### Technical Contract for Mobile

- `100dvh` for all full-screen viewport calculations (not `100vh`)
- `env(safe-area-inset-*)` padding applied to the ambient background layer
- `touch-action: manipulation` on all interactive elements (eliminates 300ms tap delay)
- `overscroll-behavior: contain` on the menu scroll container only
- `position: sticky` never used inside `overflow: hidden` containers
- The arrival screen suppresses all visible browser chrome via `meta viewport` and `display: standalone` patterns

---

## PART VIII — THE LETTER ARCHITECTURE

Every major section of the guest experience corresponds to a form of correspondence. This is not metaphor. This is **structural taxonomy**. The language, the visual treatment, and the interaction model of each section derive from its correspondence type.

| Guest Journey Section | Correspondence Form | Emotional Register |
|---|---|---|
| QR Arrival | The sealed envelope arriving at table | Anticipation, warmth, presence |
| Language/welcome | The opening of the envelope | Recognition, invitation |
| Mode selection | The invitation letter (choosing your evening) | Intimate authorship |
| Menu scroll | The tasting letter (multiple notes, one per drink) | Discovery, unhurried curiosity |
| Drink detail | The sommelier's personal note (one drink, in full) | Intimacy, consideration, desire |
| Adding to tasting | The act of placing a reply | Ceremony, decision, pleasure |
| Tasting tray | The composed tasting menu (your selection, presented) | Authorship, anticipation |
| Sending the order | Sealing and dispatching the correspondence | Ceremony, ritual, relief |
| Confirmation | The sealed acknowledgement (received with grace) | Warmth, belonging, completion |
| Idle waiting state | The table presence (a letter already sent, a reply being prepared) | Rest, ambient beauty, trust |

### The Language Map

Every word of copy in ANNAP is a line in one of these letters. It must sound like the person writing the letter — a thoughtful, unhurried sommelier who loves what they do and trusts the guest to appreciate nuance.

| Section | Voice Register |
|---|---|
| Arrival | Warm, quiet, welcoming. No questions asked. |
| Mode selection | Personal, intuitive, knowing. As if the sommelier already understands. |
| Menu | Evocative, sensory. Leading with feeling, not description. |
| Drink detail | Intimate, specific, unhurried. A recommendation from a trusted friend. |
| Tray | Editorial, composed, proud. Presenting a curation. |
| Confirmation | Warm, assured, grateful. A host's genuine pleasure. |
| Idle | Ambient, almost silent. Single lines, like notes left on a table. |

---

## PART IX — THE MOTION SYSTEM

### The Single Governing Motion Metaphor

All motion in ANNAP moves like **paper being handled with care**.

- Paper unfolds slowly from a fold — it does not spring open.
- Paper settles with slight authority when placed — it does not bounce.
- Paper lifts with intention when picked up — it does not jerk.
- Paper dissolves back into a surface gently — it does not snap.

Every animation must pass this test: *Does this feel like paper being handled with care?*

### The Motion Vocabulary

**Unfold** — Elements arriving into the guest's view. Slow entry, deliberate settle. Uses `--ease-unfold`. Duration: `480ms` standard, `680ms` for larger surfaces, `900ms` for ceremonial moments.

**Fold** — Elements departing. Quick enough to not delay the guest, slow enough to feel intentional. Uses `--ease-fold`. Duration: always `70%` of the corresponding unfold duration.

**Settle** — Physical placement moments (tray rising, letter landing). A firm, confident motion with the faintest overshoot that makes it feel real. Uses `--ease-settle`. Duration: `520ms`.

**Drift** — Ambient atmospheric motion. Imperceptibly slow. The room breathing. Uses sinusoidal mathematical functions, not CSS keyframes. Duration: `4000ms–45000ms` loops.

**Still** — The deliberate pause between motions. The beat before a sommelier speaks. Duration: `120ms–400ms` depending on ceremony level. Never skip the still moment.

### The Still Moment — Mandatory Rule

Between every two successive animations in a scene transition, there is a mandatory still moment. During this moment, nothing moves. The guest's eye settles. The next motion begins from a state of rest.

Duration: `120ms` minimum. `400ms` for major transitions.

This rule is non-negotiable. It is what separates a cinematic experience from a busy interface.

### What Does Not Move

In luxury motion design, **stillness is the signal of confidence**.

Once content arrives, it is still. Typography does not wiggle on hover. Images do not scale on hover. Action elements do not transform aggressively on focus. The world is at rest, and the guest moves through it.

Moving elements at any given moment: maximum 2 simultaneously animating surfaces. Ambient background drift does not count toward this limit (it is perpetual and imperceptible). Grain texture never animates. Typography never animates while the guest is reading it.

---

## PART X — THE COLOR PHILOSOPHY

### The Palette Universe

ANNAP lives inside four tonal families:

**Warm paper** — the surface and ground of all experience
```
--paper-warm:    #F5EFE3   /* Primary surface — warm ivory */
--paper-deep:    #EDE3D4   /* Slightly richer — layered depth */
--paper-aged:    #E3D6C4   /* Envelopes, older correspondence */
--paper-shadow:  #D4C4AE   /* Folds, edge shadows, letter creases */
```

**Deep ink** — the primary typographic register
```
--ink-deep:      #2C2118   /* Primary text — warm near-black, never cold */
--ink-body:      rgba(44, 33, 24, 0.85)   /* Narrative text */
--ink-medium:    rgba(44, 33, 24, 0.55)   /* Secondary, functional */
--ink-faint:     rgba(44, 33, 24, 0.30)   /* Ghost, atmospheric labels */
--ink-trace:     rgba(44, 33, 24, 0.12)   /* Divider lines, texture marks */
```

**Wax seal tones** — the accent register, used with extreme restraint
```
--wax-seal:      #8B3A28   /* The single accent — deep wax red */
--wax-warm:      #A04830   /* Warmer expression, candlelit */
--wax-amber:     #C47832   /* Candlelit highlight, very rare use */
```

**Atmospheric depth** — for the ambient background layer and mode atmospheres
```
--depth-void:    #0A0806   /* The room when the letter isn't open */
--depth-warm:    #1A1410   /* Ambient candlelight background */
--depth-ember:   #241C14   /* Slightly lifted — elevated dark surfaces */
```

### The Color Rules

1. **Paper is the default surface.** Most of the guest experience lives on warm paper, not dark depth. Dark depth is used for: arrival bloom, ambient atmospheric background, mode atmosphere. Paper is used for: all letter content — menus, drink details, the tasting tray.

2. **Wax is used once per screen, maximum.** The wax accent exists to mark singular moments of ceremony. It may appear on: the wax seal indicator, the "Send to the bar" action at the precise moment of confirmation, the tray indicator line when active. Never as a decorative element. Never as a color in a palette that repeats across components.

3. **No bright colors.** The ANNAP palette is deliberately warm and muted. Any color that reads as "vivid" or "saturated" does not belong here. This includes: teal accents, bright orange call-to-action buttons, royal blue, any neon tone, any pure white.

4. **Warm white, never pure white.** `#FFFFFF` does not exist in ANNAP. All light surfaces are warm. The minimum warmth is `--paper-warm: #F5EFE3`.

5. **Dark means warm dark.** All dark tones in ANNAP are warm darks — with red/amber in their base, never gray or blue-black. `--ink-deep: #2C2118` has warmth. `#1A1A1A` is prohibited.

6. **Atmospheric depth for transitions only.** The `--depth-*` palette appears during: the arrival sequence, major scene transitions, the confirmation bloom, the idle ambient state. Not in the content experience itself. The guest moves from depth into warmth as they enter the correspondence.

### The Mode Atmosphere System

Mode selection subtly shifts the atmosphere, not the palette. The palette is fixed. The atmosphere is weighted:

| Mode | Atmosphere Shift |
|---|---|
| Solo | Paper warms slightly cooler — `--paper-deep` becomes the primary surface instead of `--paper-warm`. Typography tightens fractionally. Silence increases. |
| Group | Paper warms — `--paper-warm` becomes richer, amber accents breathe more warmly. Typography feels slightly more generous. |
| Adventurous | The ambient background beneath the paper surface shifts toward `--depth-ember` — a sense of something alive underneath. Paper remains warm, but the world beneath it has more energy. |

---

## PART XI — THE ANNAP FEELING

### The Question

*What should a guest emotionally feel within the first 15 seconds after scanning the QR code?*

### The Answer

In the first 15 seconds, the guest should feel exactly one thing:

**They have been noticed.**

Not in the surveillance sense. In the hospitality sense. The way you feel when you walk into a room and a thoughtful host turns from across the space, makes eye contact, and gives the smallest nod that says: *I see you. You are welcome here. We are glad you came.*

They should not feel:
- Impressed by technology
- Stimulated by visual effect
- Directed toward action
- Invited to browse
- Prompted to choose

They should feel:
- Received
- Unhurried
- Trusted with something private
- In the presence of taste
- Slightly privileged — as if this moment was prepared specifically for them

The product should feel, in those 15 seconds, like the most personal digital hospitality experience they have ever encountered. Not because of spectacle. Not because of novelty. Because someone took the time to write them a letter.

That is the ANNAP feeling.

That is what we are building.

---

## APPENDIX A — DECISION FRAMEWORK

When any implementation decision arises that is not explicitly covered by this document, apply the following test in sequence:

**1. The Letter Test:** Does this decision make the experience feel more like receiving a private letter, or more like using an app?

**2. The Sommelier Test:** If the world's best sommelier were hosting this experience in person, would they make this choice?

**3. The Silence Test:** Does this decision add or remove silence and stillness from the experience?

**4. The Paper Test:** Does this visual element feel like it belongs on warm paper, written in careful ink?

If any answer to these tests is unfavorable, the decision is wrong. Return to a simpler path.

---

## APPENDIX B — REFERENCE HIERARCHY

When this document conflicts with earlier documents, **this document governs**. When this document is silent on a specific implementation detail, consult in this order:

1. `ANNAP_IMPLEMENTATION_DOCTRINE.md` — for precise motion timing, easing values, choreography sequences
2. `ANNAP_EXPERIENCE_BLUEPRINT.md` — for emotional pacing, scene structure, and hospitality philosophy
3. `ANNAP_PHASE0_UI_AUTHORITY.md` — for token naming conventions and Tailwind/CSS architecture

No implementation proceeds without one of these four documents as its explicit authority.

---

*ANNAP Unified Identity Doctrine v1.0*
*This document is complete. It does not require revision — it requires adherence.*
*Implementation begins from here.*
