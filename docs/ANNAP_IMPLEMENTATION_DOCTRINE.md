# ANNAP — Complete Frontend Refactor Blueprint
*Production Implementation Document v1.0*
*Lead Experience Director & Frontend Systems Architect*

---

## PART 1 — EXPERIENCE ARCHITECTURE

### The Screen Sequence as Emotional Narrative

ANNAP's guest journey is not a funnel. It is a score — a piece of music in nine movements, each with its own tempo, its own emotional color, its own reason to exist. What follows is the definitive architecture of every scene in that score.

---

### SCENE 01 — QR ARRIVAL

**Emotional goal:** The guest should feel *received*, not redirected.

**Duration:** 3.2 seconds minimum before any interaction is possible.

**What MUST be visible:**
The ANNAP wordmark — large, hair-thin weight, absolute center of screen. Below it, after a 1.4-second pause: a single line of welcoming copy in the narrative register. Nothing else. No navigation. No menu. No buttons. No status indicators.

**What MUST disappear:**
Everything that communicates "software." The browser chrome should be suppressed via viewport meta and `display: standalone` behavior where possible. No visible URL bar context. No loading progress indicators of any kind.

**Choreography sequence — exact timing:**

```
0ms       → Background ambient gradient blooms from black center outward
            Duration: 1800ms, easing: cubic-bezier(0.16, 1, 0.3, 1)

400ms     → ANNAP wordmark materializes via opacity + 3px vertical rise
            Duration: 1200ms, easing: cubic-bezier(0.25, 0.46, 0.45, 0.94)

1600ms    → [Still moment — 400ms of complete stillness]

2000ms    → Welcome line fades in beneath the wordmark
            Duration: 800ms, opacity only, no movement

2800ms    → [Still moment — 400ms]

3200ms    → System becomes interactive. Wordmark begins slow ambient
            breathing cycle (scale 1.0 → 1.008 → 1.0, 6s loop)
```

**Ambient behavior:** The background is a radial gradient — deep, almost-black warm amber at the edges, a fractionally lighter warmth at the center. This gradient itself performs a very slow rotational drift — approximately 0.3° per second — creating the sensation of being in a room where the light source is alive.

**Typography behavior:** Wordmark at 52px, tracking +0.18em, font-weight 100. Welcome line at 15px, tracking +0.08em, font-weight 300, color at 55% opacity of the primary warm tone.

**Mobile behavior:** On devices where the QR scan happens inside an app browser (Instagram, LINE), suppress any visible top bar contamination using a `padding-top: env(safe-area-inset-top)` wrapper. The wordmark should always appear vertically centered in the *visible* viewport, not the document height.

---

### SCENE 02 — MODE SELECTION

**Emotional goal:** The guest makes their first act of authorship. They choose how tonight feels.

**What MUST be visible:**
One mode at a time. Not three cards. Not a grid. One mode, full screen, with a name, an atmospheric subline, and a single gesture invitation. The transition between modes is lateral — a slow horizontal drift, like pages turning in a beautiful book.

**What MUST disappear:**
The ANNAP wordmark reduces to a very small, very faint presence in the upper-left corner — a discreet watermark, not a header. No navigation of any kind. No back button. No progress indicator.

**The three mode screens — individual definitions:**

*Just for You (Solo)*
Background shifts to a cooler, quieter tone — deep slate with warm amber ambient light pooling softly at the lower third of the screen. The typography becomes more intimate, slightly smaller, more closely tracked. The atmospheric subline: *"Your time. Your pace. Your pleasure."* A single tap anywhere on the screen proceeds. There is no button.

*Shared Pleasures (Group)*
Background warms significantly — deep amber moves toward a richer, more saturated copper. The typography grows fractionally bolder. The atmospheric subline: *"The best things are better together."* The tap target is generous, full-screen.

*Surprise Me (Adventurous)*
This mode is different. The background does not settle — it shifts slowly between two unusual tones (deep teal and warm amber, bleeding at the center). The typography uses a very slightly larger scale. The atmospheric subline: *"Trust us. We know what you need."* On this screen, a gentle, unpredictable ambient particle behavior activates — very sparse, very slow-moving luminous points that drift upward. This is the only screen where ambient elements move independently.

**Transition between modes:** The guest swipes horizontally or a slow auto-advance timer presents each mode in sequence (8-second dwell, looping). The transition between mode screens uses a 50% overlapping crossfade — the outgoing screen's opacity falls while the incoming screen rises, with a 600ms duration and the ANNAP easing curve. Color temperature of the background shifts simultaneously, taking the full 600ms to complete.

**Mode selection confirmation:**
When the guest commits to a mode (tap, double-tap, or a slow vertical swipe upward), their choice triggers the most important transition in the journey. The selected mode's atmospheric color becomes the foundation for everything that follows. This color does not disappear — it lives in the ambient layer for the entire session.

---

### SCENE 03 — MENU DISCOVERY

**Emotional goal:** The guest encounters the world of ANNAP's offerings as an *experience*, not a list.

**Arrival choreography:**
The mode screen does not navigate away. It *transforms*. The atmospheric text dissolves. The full-screen atmospheric background compresses upward, becoming the ambient header layer — a thin atmospheric band at the top of the screen that persists and breathes. From below, the first menu item rises into view, large and unhurried.

**Scroll behavior:**
The menu scroll is the guest's primary movement through the experience. Each drink occupies a significant vertical space — minimum 65% of the viewport height per item. The scroll is continuous, momentum-based, and uses native iOS/Android inertia. Between items, there is a deliberate 24px atmospheric gap — not empty space, but a continuation of the ambient background layer that breathes briefly before the next item arrives.

**Per-item visual anatomy:**
- Full-bleed atmospheric image at 60% of item height
- Drink name in display register: 28px, weight 200, tracking +0.06em, entering from a 6px vertical offset as the item scrolls into the visible zone
- Sommelier descriptor line in narrative register: 13px, weight 300, tracking +0.04em, opacity 65%, arriving 200ms after the name
- Occasion tag in functional register: 10px, weight 500, tracking +0.12em, uppercase, opacity 40%, arriving 400ms after name
- Price in functional register: right-aligned, same scale as occasion tag

**What is NOT visible during menu browsing:**
No persistent cart icon during initial browsing (first 60 seconds of menu time). No category filter tabs. No search bar. No promotional banners. No star ratings. No review counts. No "popular" badges unless they appear as part of a contextual atmospheric overlay, not a UI element.

**Sommelier mode:** After the guest has scrolled past three items without tapping, a single line of atmospheric copy drifts upward from the bottom edge of the screen: *"Not sure where to begin?"* If the guest ignores it, it fades after 4 seconds. If they tap it, the Adventurous guidance layer activates — a brief, personalized sommelier suggestion based on mode context.

---

### SCENE 04 — DRINK DETAIL

**Emotional goal:** Intimacy. The guest leans in. The world narrows to this one thing.

**The expansion behavior:**
The drink detail does not navigate to a new page. It expands — the item's image grows to fill the upper 55% of the screen while the content area below scrolls upward, revealing the full character of this drink. The URL should update (for sharing and browser history) without a visible page transition. Use the History API with a morphing view transition, not a route change.

**What MUST be visible upon expansion:**
- The drink at near-full-screen atmospheric scale (image fills width, atmospheric treatment applied — subtle vignette at edges, slight desaturation except for the drink itself)
- The name in display register, large, arriving 300ms after the expansion completes
- A single, most evocative sensory phrase: *"The first thing you'll taste"* — one sentence, not a bullet list
- Below that, the full sommelier character note — 2–4 sentences, narrative register, unhurried
- Further below (requiring a small scroll within the detail): occasion context, pairing suggestions if any, and the ceremonial add action

**What MUST disappear:**
Everything from the menu list. The guest is now in a one-to-one conversation with this drink. No menu chrome should be visible.

**The ceremonial add action:**
Located at the bottom of the detail content — always reachable without extraordinary effort, never immediately visible upon expansion (the guest must scroll into it). The action element is large, full-width, typeset with the hospitality vocabulary: *"Add to your tasting."* Its visual treatment is atmospheric rather than button-like — a faint, warm-toned area that pulses once, very gently, when it first comes into view (scale 1.0 → 1.01 → 1.0, 800ms, once only).

**Collapse behavior:**
A slow downward swipe gesture collapses the detail, re-compressing the item back to its menu position. The list scroll position is preserved exactly. The transition is the visual reverse of the expansion, taking 450ms.

---

### SCENE 05 — TASTING TRAY

**Emotional goal:** The guest reviews their composition with pride. This is their authorship moment.

**Invocation:** A slow upward swipe from below the screen's bottom 48px zone, or a tap on the very discreet ambient tray indicator (a thin warm line at the very bottom of the screen, visible only after the first item has been added — not a cart badge, not a number, just a warm presence indicating something is waiting there).

**The tray rises:**
Duration 520ms, easing: spring-like settle using `cubic-bezier(0.34, 1.56, 0.64, 1)` — firm and decisive, with just the faintest overshoot that makes it feel physical. It rises to cover 78% of the screen from the bottom, leaving the top ambient layer visible above it. The tray's background is a deep, warm frosted surface — not aggressive glassmorphism, but a contained, beautiful use of backdrop-filter: blur(32px) over a very dark warm base color.

**Tasting tray anatomy:**
- Header: *"Your table"* in display register, centered, 22px, weight 200
- Each item listed as an editorial moment — name, quantity if more than one, individual price. Generous line height. No delete icons cluttering each row (a left-swipe gesture reveals a minimal remove option)
- Total expressed as: *"This evening's selection — [price]"*
- Single action: *"Send to the bar"* — full width, atmospheric treatment

**What MUST NOT appear:**
Promo code fields. Upsell suggestions within the tray itself. Multiple payment options (payment is at-table, handled by staff). Any e-commerce pattern.

---

### SCENE 06 — ORDER CONFIRMATION

**Emotional goal:** Relief. Warmth. The sense that something is now in capable hands.

**Choreography:**
The "Send to the bar" tap produces a 200ms moment of complete screen stillness — no animation, no feedback. Then a very slow, warm bloom fills the screen from the tap point outward, reaching full coverage in 1200ms. The tray dissolves within this bloom. When the bloom settles, the confirmation scene is present.

**What is visible:**
- The ANNAP wordmark, returned to center at medium scale, the same position as the arrival scene. This is intentional — the journey has a beginning and an end that rhyme.
- Below it, the confirmation copy — unhurried, warm, grateful: *"Your tasting is with us. Sit back — it's our pleasure from here."*
- Below that, quietly: a table identifier expressed atmospherically: *"Table [number]"* in functional register
- Estimated wait expressed without pressure: *"Arriving in [window]"* if the POS system can surface this

**What is NOT visible:**
An order number. A receipt-style item list. Any "track your order" pattern. The guest does not need to manage this — they are guests, not logistics operators.

---

### SCENE 07 — WAITING STATE / TABLE PRESENCE

**Emotional goal:** The screen becomes a living environmental element — a digital candle.

**The idle ambient state:**
After 90 seconds of inactivity post-confirmation, the screen transitions to the table presence mode. The ANNAP wordmark at very large, extremely fine weight, absolute center. The ambient background performs its slow rotational gradient drift. Very occasional — every 30–45 seconds — a single atmospheric line drifts into view and dissolves: *"Arriving soon."* or *"Your bar is preparing."* The table number persists in the lower third, minimal.

**Tap behavior in idle state:**
A tap anywhere reveals a discreet radial menu with two options only — *"Add something"* (returns to menu) and *"Call for assistance"* (flags table to staff system). These options appear for 6 seconds and then dissolve back into the ambient field.

---

## PART 2 — VISUAL SYSTEM

### Color Philosophy

ANNAP's color universe is built on the principle of **thermal depth** — color that feels like it has a temperature, a physical presence, a light source behind it.

**The foundational palette:**

```css
/* Atmospheric Foundation */
--annap-void:        #0A0806;   /* Near-black warm — the deepest ambient */
--annap-depth:       #12100D;   /* Background base — warm, never cold */
--annap-dusk:        #1C1712;   /* Elevated surfaces */
--annap-ember:       #2A2018;   /* Cards, panels — warmth perceptible */

/* Atmospheric Warmth — the soul of the palette */
--annap-amber:       #C8843C;   /* Primary warm tone — used sparingly */
--annap-gold:        #D4A043;   /* Accent — even more sparingly */
--annap-copper:      #9A6232;   /* Tertiary warm — for group mode */
--annap-slate:       #3A4048;   /* Solo mode cool tone */
--annap-teal:        #1A3035;   /* Adventure mode — deep surprise */

/* Text — never pure white */
--annap-text-primary:   rgba(245, 238, 228, 0.92);  /* Warm off-white */
--annap-text-secondary: rgba(245, 238, 228, 0.55);  /* Narrative register */
--annap-text-tertiary:  rgba(245, 238, 228, 0.35);  /* Functional register */
--annap-text-ghost:     rgba(245, 238, 228, 0.18);  /* Ambient UI elements */
```

**Mode atmospheric overlays — applied to the base palette:**

Solo mode applies a `-5°` Kelvin shift on all warm tones and introduces `--annap-slate` as an accent. Group mode amplifies amber saturation by approximately 20% and warms all background tones. Adventurous mode introduces the teal as a mid-layer ambient color that drifts slowly between teal and amber.

### Atmospheric Gradients

The master ambient gradient — used as the living background across the entire session:

```css
.annap-ambient-background {
  background: radial-gradient(
    ellipse 80% 60% at 50% 40%,
    rgba(28, 20, 10, 1) 0%,
    rgba(10, 8, 6, 1) 100%
  );
  /* This gradient slowly rotates via JS-driven CSS custom property */
}
```

The rotation is implemented not with a CSS animation on the gradient itself (which forces repaint) but via a single JavaScript `requestAnimationFrame` loop that updates two CSS custom properties: `--gradient-x` and `--gradient-y`, moving the ellipse center in a slow sinusoidal path. The full cycle takes 45 seconds. This is imperceptible as animation — it simply makes the room feel alive.

### Grain Overlay

A single, persistent grain texture overlay sits at the top of the z-index stack (below only modals and critical overlays), at `pointer-events: none`, `position: fixed`, covering the full viewport. Opacity: 0.04. This is a pre-generated PNG grain tile, `background-repeat: repeat`, size 200×200px. It does not animate. It is simply present — giving the screen a tactile, material quality that makes it feel less like glass and more like paper.

### Glassmorphism — Restraint Definition

Glassmorphism in ANNAP is used only for surfaces that exist *above* the atmospheric background — the tasting tray, contextual tooltip moments, and the mode selection transition overlay. Rules:

- `backdrop-filter: blur()` values: maximum 32px, never applied to elements that scroll (performance catastrophe on mobile)
- The glass surface must always sit above a sufficiently complex background to make the blur meaningful — never over a flat surface
- Glass surfaces use `background: rgba(var(--annap-ember-rgb), 0.72)` — the blur is secondary to the color foundation beneath it
- Never more than one glass surface visible simultaneously

### Lighting Direction

All ANNAP surfaces are lit from *above-center* — a warm, soft, diffuse light source. This means:
- Shadows fall downward and outward, never upward
- The topmost edges of surfaces carry a very faint warm highlight (1px, 8% opacity)
- Images within the experience receive a gradient overlay that darkens at their edges and brightens slightly at their upper-center — consistent with the lighting model

### Shadow Philosophy

Shadows in ANNAP are **not** drop-shadows. They are **depth indicators** — evidence that surfaces exist in dimensional space.

```css
/* Depth level 1 — surface elevation (tray, panels) */
box-shadow: 0 24px 80px rgba(0,0,0,0.72), 0 4px 16px rgba(0,0,0,0.48);

/* Depth level 2 — deep emergence (confirmations, ceremonies) */  
box-shadow: 0 48px 120px rgba(0,0,0,0.88), 0 8px 32px rgba(0,0,0,0.64);

/* Ambient glow — warm accent surfaces only */
box-shadow: 0 0 40px rgba(200, 132, 60, 0.12);
/* Used on: the tasting tray header, the confirmation wordmark */
/* Never: on menu items, text, or navigational elements */
```

### Spacing System

ANNAP uses a base-8 spacing system with atmospheric exceptions:

```
4px   — micro: internal element breathing
8px   — small: between related elements
16px  — unit: standard component padding
24px  — breath: between menu items (the atmospheric gap)
32px  — section: between content zones within a screen
48px  — stage: primary content padding from screen edges
64px  — ceremonial: used for the arrival and confirmation scenes
96px  — atmospheric: the silence before major moments
```

The horizontal margin of the primary content zone on mobile is `24px` — never the standard `16px` used in most apps. Those 8 extra pixels per side are not significant in pixels. They are significant in *feeling*.

### Border Radius System

```
2px   — functional elements only (price tags, occasion chips)
8px   — contained surfaces (detail panels)
16px  — elevated surfaces (tray, overlays)
24px  — expressive surfaces (mode cards)
full  — used nowhere except circular ambient indicators
```

### Typography Scale

```css
/* Display Register — editorial presence */
.type-display {
  font-size: clamp(36px, 8vw, 52px);
  font-weight: 200;
  letter-spacing: 0.06em;
  line-height: 1.1;
  color: var(--annap-text-primary);
}

/* Title Register — section authority */
.type-title {
  font-size: clamp(22px, 5vw, 28px);
  font-weight: 300;
  letter-spacing: 0.04em;
  line-height: 1.2;
  color: var(--annap-text-primary);
}

/* Narrative Register — drink descriptions, sommelier voice */
.type-narrative {
  font-size: 15px;
  font-weight: 300;
  letter-spacing: 0.02em;
  line-height: 1.7;
  color: var(--annap-text-secondary);
  max-width: 32ch; /* intimacy constraint */
}

/* Functional Register — prices, categories, labels */
.type-functional {
  font-size: 11px;
  font-weight: 500;
  letter-spacing: 0.12em;
  text-transform: uppercase;
  color: var(--annap-text-tertiary);
}

/* Ghost Register — ambient UI, idle state elements */
.type-ghost {
  font-size: 10px;
  font-weight: 400;
  letter-spacing: 0.10em;
  text-transform: uppercase;
  color: var(--annap-text-ghost);
}
```

### Z-Index Philosophy

The layering system is a deliberate stack of reality — from the physical room at the bottom to the guest's immediate focus at the top.

```
z-0    → ambient background (the room)
z-10   → atmospheric grain overlay
z-20   → ambient background animation elements
z-30   → menu content (the world within the room)
z-40   → persistent ambient markers (tray indicator line)
z-50   → tasting tray (elevated surface)
z-60   → drink detail expansion
z-70   → mode selection atmosphere
z-80   → ceremonial overlays (confirmation bloom)
z-90   → arrival choreography
z-100  → system-critical elements only
```

---

## PART 3 — MOTION SYSTEM

### The ANNAP Easing Library

Four curves govern the entire motion system. No other easing values should exist in the codebase.

```css
:root {
  /* ANNAP Arrive — elements entering the space */
  /* Begins gently, settles with authority */
  --ease-arrive:   cubic-bezier(0.16, 1, 0.3, 1);
  
  /* ANNAP Release — elements leaving, dissolving */
  /* Begins with presence, fades gracefully */
  --ease-release:  cubic-bezier(0.7, 0, 0.84, 0);
  
  /* ANNAP Settle — physical placement, tray rise */
  /* Firm with a whisper of overshoot */
  --ease-settle:   cubic-bezier(0.34, 1.56, 0.64, 1);
  
  /* ANNAP Drift — ambient, atmospheric, background motion */
  /* Linear with imperceptible sinusoidal variation via JS */
  --ease-drift:    linear;
}
```

### Duration Framework

```
Instant:      0ms   — state changes that must not animate (a11y: prefers-reduced-motion)
Micro:      120ms   — tap feedback, selection state changes
Brief:      280ms   — component state changes, show/hide of contextual elements
Standard:   450ms   — panel transitions, detail collapse
Deliberate: 650ms   — scene transitions between major zones
Ceremonial: 900ms   — mode selection, confirmation bloom, arrival elements
Ambient:    4000ms+ — breathing cycles, background drift, idle motion
```

### Scene Transitions — Exact Specifications

**Arrival → Mode Selection:**

```
Duration: 900ms
Behavior: Arrival content (wordmark, welcome line) fades with --ease-release
          over first 400ms. At 200ms (overlapping), mode screen background
          begins to bloom in from center with --ease-arrive over 700ms.
          First mode card content staggers in: name at 600ms, subline at 800ms.
          Result: The arrival dissolves into the mode world. No hard cut.
```

**Mode Selection → Menu Discovery:**

```
Duration: 1100ms (the longest, most important transition)
Behavior: Mode atmosphere compresses upward over 600ms with --ease-arrive.
          Becomes the ambient header band at 32px height.
          Simultaneously, first menu item rises from 40px below final position,
          opacity 0→1, over 700ms beginning at 400ms mark.
          Second menu item (partially visible below fold) rises at 200ms stagger.
          Background color temperature shift completes within 600ms.
```

**Menu Item → Detail Expansion:**

```
Duration: 520ms
Behavior: Selected item's image scales up from current position to fill
          upper viewport using CSS clip-path animation or transform/scale.
          Surrounding items fade to 0 opacity over 300ms.
          Detail content rises from below fold over 520ms with --ease-arrive.
          URL updates via History.pushState at 260ms (midpoint, invisible).
```

**Detail Collapse:**

```
Duration: 450ms
Behavior: Exact reverse, but 15% faster — departure should always feel
          slightly quicker than arrival. --ease-release governs the image
          compression. List items fade back in at 200ms mark.
```

### Element Stagger System

When multiple elements enter a scene simultaneously, they must never arrive together.

```javascript
// Stagger sequencing constants
const STAGGER = {
  tight:   80,   // Related elements: name → subline → price
  standard: 150, // Menu item components
  generous: 220, // Mode screen elements
  ceremonial: 300 // Arrival and confirmation elements
};
```

### Cart Addition Ceremony — Exact Motion

This is the most important micro-interaction in the product. It must be implemented with precision.

```
Trigger: Guest taps "Add to your tasting"

0ms     → Tap point emits a single, slow radial pulse:
           width 0 → 60px, opacity 0.3 → 0, duration 600ms, --ease-arrive
           Color: var(--annap-amber), filter: blur(8px)

80ms    → The action element label text cross-fades:
           "Add to your tasting" → "Added with care"
           Duration: 320ms, opacity cross-fade only

200ms   → A soft-focus ghost of the drink name appears at the tap location:
           opacity 0 → 0.6 → 0 over 800ms, tracking expanding from 0 → 0.08em
           This is the drink "departing" toward the tray

400ms   → The ghost reaches the tray indicator at screen bottom:
           scale reduces from 1 → 0.1 as it travels
           The tray indicator line warms in color (opacity pulse)

1000ms  → Complete. The action element returns to resting state.
           A new option appears: "Continue exploring" fades in beneath the action.
```

### Idle Breathing System

During the waiting state, the screen must feel alive without feeling active.

```javascript
// Ambient breathing — the wordmark in idle state
// Implemented via requestAnimationFrame, not CSS animation
// to allow JS-controlled pause on user interaction

const breathe = {
  scale:    { min: 1.000, max: 1.006, period: 7200 },  // 7.2s cycle
  opacity:  { min: 0.85,  max: 0.95,  period: 9000 },  // 9s cycle, offset from scale
  // Different periods prevent the two from ever being in sync —
  // creates organic, non-mechanical feeling
};
```

---

## PART 4 — INFORMATION SIMPLIFICATION

### What Disappears Entirely

**Persistent category navigation.** Menu categories are an information architecture artifact, not a hospitality experience element. The guest does not need to manage categories. If the menu is curated and not excessively long, linear discovery serves them better. If the menu is long, categories become an invoked contextual layer — accessible via a held gesture on the menu's ambient header band, appearing as a translucent overlay of five or fewer mood-categories (not ingredient types, not preparation methods — *moods*).

**Visible cart badge/counter during initial menu browsing.** For the first sixty seconds of menu time, there is no visible tray indicator. The tray indicator line appears only after the first item has been added. This removes the constant merchant-side pressure of "you have 0 items." The guest is exploring, not counting.

**The language toggle.** Language is selected once, at the moment of QR arrival, as part of the welcome — before the arrival choreography completes. It is never visible again during the session. If a guest later needs to change it, a long-press on the ambient wordmark at the top of any screen reveals this option, along with nothing else.

**Star ratings and review counts.** These are borrowed from e-commerce and have no place in a hospitality context. Sommelier voice replaces social proof.

**"Popular" and "Recommended" badges.** Replace with the sommelier suggestion system — contextual, mode-aware, narrative, and invoked rather than persistent.

**Any visible loading state.** Loading should be invisible. Either the next piece of content is already in memory (prefetched on scroll proximity), or the ambient transition itself provides the perceptual cover for any brief data retrieval. A visible spinner is a failure of hospitality — it is a host saying "wait" rather than keeping the conversation flowing.

### What Becomes Contextual

**The sommelier guidance layer.** Present only when the guest has paused in scrolling for more than 3 seconds (indicating consideration, not passage), or when invoked by the guest's interaction with the guidance trigger.

**Category navigation.** Behind a long-press gesture on the menu header. Appears as an atmospheric overlay. Disappears after selection.

**Service call.** Accessible only within the idle table presence state. Not a persistent button anywhere in the ordering flow.

**The tray/cart.** Exists only after items are added to it. Invoked by swipe gesture. Returns to ambient state after being closed.

### What Becomes Atmospheric Instead of Explicit

**Table identification.** Instead of "Table 7" as a label, the ambient welcome copy should incorporate the table identity: *"Welcome to your corner of ANNAP."* or simply have the table number present in the arrival as the most minimal possible environmental indicator — typeset in `type-ghost` at the very bottom of the arrival screen.

**Time and wait indication.** Never shown as a timer or progress bar. Expressed as human copy only: *"Arriving shortly"* — updated by the staff system without any visual mechanism that resembles a countdown.

**Order status.** The confirmation state is the final guest-facing state. What happens between order confirmation and drink arrival is not the guest's responsibility to track. The staff handles this entirely. The waiting state reflects completeness, not progress.

---

## PART 5 — IMPLEMENTATION PLAN FOR CURSOR

### Phase 1 — Foundation
*Priority: Critical | Mobile Risk: Low | Duration: 2–3 days*

**Files involved:**
- `wwwroot/css/app.css` or `tailwind.config.js`
- `Pages/Shared/_Layout.cshtml`
- `wwwroot/js/core/ambient.js` (new file)

**What must change:**
Establish the CSS custom property system — the full color palette, spacing scale, easing curves, and z-index stack as CSS variables. This is the single source of truth for everything that follows.

Configure Tailwind to reference these custom properties via `extend` — do not replace Tailwind's utility system, extend it with ANNAP-specific tokens.

Create the ambient background layer as a single fixed `div` at `z-10` in the layout. This is the persistent room. It must be present across all page navigations.

Implement the grain overlay — a fixed, pointer-events-none element at `z-10` above the ambient background, containing the grain texture as a `background-image`.

**What must be removed:**
Any global default margins, paddings, or background colors set on `body` or `html` that will conflict with the edge-to-edge atmospheric design. Any Tailwind reset classes that produce white or light-gray backgrounds on components.

**Performance considerations:**
The ambient background gradient rotation must use `transform` or `opacity` only — never `background-position` animation or `background-size` animation, both of which trigger layout recalculation. The sinusoidal drift should run in a `requestAnimationFrame` loop updating CSS custom properties only.

---

### Phase 2 — Arrival Experience
*Priority: Critical | Mobile Risk: Medium | Duration: 3–4 days*

**Files involved:**
- `Pages/Index.cshtml` (the QR landing — likely currently the menu or a redirect)
- `wwwroot/js/arrival/choreography.js` (new file)
- `Pages/Shared/_ArrivalOverlay.cshtml` (new partial)

**What must change:**
The QR landing page must become a dedicated arrival scene. If it currently redirects to a menu or mode selection immediately, this redirect must be replaced with the arrival choreography sequence.

The arrival choreography is a JavaScript orchestration class that manages the timed sequence of DOM class additions — bringing elements into visibility according to the timing framework defined above. It uses a lightweight Promise-chain approach:

```javascript
// pseudocode for Cursor
class ArrivalConductor {
  async perform() {
    await this.bloomBackground();     // 1800ms
    await this.wait(400);             // partial overlap
    await this.materializeWordmark(); // 1200ms
    await this.wait(400);             // still moment
    await this.fadeWelcomeLine();     // 800ms
    await this.wait(400);             // final breath
    this.enableInteraction();
  }
}
```

Language detection and selection must happen during the arrival, before the choreography completes. If the device language suggests Vietnamese, the welcome line appears in Vietnamese. If ambiguous, both appear briefly — Vietnamese above, English below — and the guest taps their language. This selection persists in sessionStorage.

**What must be removed:**
Any page elements that make the arrival feel like a web page — visible browser-context UI, any flash of default styling (ensure `background-color: var(--annap-depth)` is set on `html` to prevent white flash), any visible navigation on initial load.

**Mobile risk:**
iPhone Safari's address bar behavior affects viewport height. Use `window.visualViewport` for height calculations rather than `100vh`. The arrival wordmark vertical centering must account for this.

---

### Phase 3 — Mode Atmospheres
*Priority: Critical | Mobile Risk: Medium | Duration: 4–5 days*

**Files involved:**
- `Pages/ModeSelection.cshtml` (may be a new page or overlay state)
- `wwwroot/js/modes/modeOrchestrator.js` (new file)
- `wwwroot/js/modes/atmosphereManager.js` (new file)
- `Pages/Shared/_ModeAtmosphere.cshtml` (new partial)

**What must change:**
Mode selection must be rebuilt as a full-screen sequential experience. Three "scenes" exist as stacked DOM elements, each occupying the full viewport, with transitions between them managed by the `ModeOrchestrator` class.

The `AtmosphereManager` is responsible for the most critical behavior in the entire application — the session-persistent color temperature shift that follows mode selection. When a mode is chosen, `AtmosphereManager.commit(mode)` writes the mode's color modifiers as CSS custom properties on the `:root` element. These properties affect all subsequently rendered elements and persist for the entire session (sessionStorage + on-page `:root` style).

```javascript
// Mode atmosphere definitions for Cursor implementation
const ATMOSPHERES = {
  solo: {
    '--mode-accent': 'var(--annap-slate)',
    '--mode-warmth': '0.85',    // multiplier on amber opacity
    '--mode-tempo':  '1.15',   // multiplier on animation durations (slower)
  },
  group: {
    '--mode-accent': 'var(--annap-copper)',
    '--mode-warmth': '1.2',
    '--mode-tempo':  '1.0',
  },
  adventurous: {
    '--mode-accent': 'var(--annap-teal)',
    '--mode-warmth': '0.9',
    '--mode-tempo':  '0.9',    // slightly faster — energy, surprise
  }
};
```

The adventurous mode particle system: implement as a `<canvas>` element at `z-20`, rendering 12–18 very faint luminous points using `requestAnimationFrame`. Each point has a slow upward drift velocity and a sinusoidal horizontal wander. Opacity: 0.08 to 0.18. This canvas is only active in adventurous mode and fades out during the menu transition.

**What must be removed:**
Any current filter-button or tab implementation of mode selection. Any mode state that lives in a URL query parameter (sessions should start cleanly each QR scan).

**Performance considerations:**
The canvas particle system must pause when the page is not visible (`document.visibilityState`). It must not run during any scene transition. Limit canvas resolution to `devicePixelRatio` of maximum 2 on any device.

---

### Phase 4 — Editorial Menu
*Priority: High | Mobile Risk: High | Duration: 5–7 days*

**Files involved:**
- `Pages/Menu.cshtml` or equivalent Razor Page
- `wwwroot/js/menu/menuScroll.js` (new file)
- `wwwroot/js/menu/scrollReveal.js` (new file)
- `Pages/Shared/_MenuItemCard.cshtml` (refactored partial)
- `wwwroot/js/menu/sommelierLayer.js` (new file)

**What must change:**
The menu Razor partial must be restructured to produce the editorial item architecture: atmospheric image zone, drink name in display register, sommelier descriptor in narrative register, occasion tag and price in functional register. Each item receives the appropriate CSS classes for the scroll-reveal animation.

`ScrollReveal` is a lightweight Intersection Observer implementation that adds reveal classes as items enter the viewport. It must use `rootMargin: '0px 0px -80px 0px'` to ensure reveals happen slightly before the element is fully visible — creating the feeling of content rising to meet the guest rather than content appearing abruptly.

The scroll reveal animation per menu item:

```css
.menu-item {
  opacity: 0;
  transform: translateY(20px);
  transition: opacity 600ms var(--ease-arrive),
              transform 600ms var(--ease-arrive);
}

.menu-item.revealed {
  opacity: 1;
  transform: translateY(0);
}

/* Stagger children via nth-child delays */
.menu-item.revealed .item-name    { transition-delay: 0ms; }
.menu-item.revealed .item-desc    { transition-delay: 120ms; }
.menu-item.revealed .item-meta    { transition-delay: 240ms; }
```

Image handling: All menu item images must be lazy-loaded with a warm blur-up placeholder — a tiny (20px) base64 version of the image blurred to 20px via CSS `filter: blur(20px)`, transitioning to the full image when loaded. This prevents the jarring appearance of missing images during scroll.

**What must be removed:**
Any grid layout for menu items — single column, full width only. Any category tabs above the menu. Any price-sorting or filter controls. Any item count indicators.

**Mobile risk — HIGH:**
Scroll performance on long lists is the primary concern. Implement virtual scrolling if the menu exceeds 20 items — using a simple DOM recycling approach where off-screen items have their images unloaded. The Intersection Observer must be debounced to prevent excessive callback firing during momentum scrolling.

The atmospheric gap between items (the `24px` ambient break) must render using `padding`, never `margin` — margin collapse behavior is unpredictable and can create visual inconsistencies.

---

### Phase 5 — Drink Detail Expansion
*Priority: High | Mobile Risk: High | Duration: 4–5 days*

**Files involved:**
- `wwwroot/js/detail/detailExpansion.js` (new file)
- `Pages/Shared/_DrinkDetail.cshtml` (new partial, rendered but hidden in menu page)
- `wwwroot/js/detail/addCeremony.js` (new file)

**What must change:**
The detail expansion should be implemented as a fixed-position overlay that animates from the position and dimensions of the tapped menu item. This requires capturing the tapped element's `getBoundingClientRect()` at the moment of tap, then animating the overlay from those coordinates to its final full-screen state using CSS `clip-path` or `transform`.

Recommended approach for Cursor: Use a single `position: fixed` detail panel that begins at `clip-path: inset(y% 0 (100-y2)% 0)` — where y and y2 correspond to the tapped item's viewport position — then animates to `clip-path: inset(0 0 0 0)`. This creates a clean expansion from the item's location without DOM position manipulation.

The History API update for deep-linking: `history.pushState({ drinkId }, '', `/menu/[slug]`)` at the midpoint of the expansion animation. Back-button behavior must collapse the detail rather than navigating backward in the browser sense — implement via `popstate` event listener.

The ceremonial add action: implement the full ceremony as described in the Motion System section. The `AddCeremony` class accepts the drink data and the tap event, orchestrates the animation sequence, then dispatches a custom DOM event `annap:drinkAdded` that the tray system listens for.

**Mobile risk — HIGH:**
`clip-path` animation triggers GPU compositing and is safe on modern iOS/Android. However, `backdrop-filter` must not be applied to the expanding element itself during animation — add it only after the animation completes (via a transition-end event handler). Animating `backdrop-filter` is expensive and will drop frames.

---

### Phase 6 — Tasting Tray Ceremony
*Priority: High | Mobile Risk: Medium | Duration: 3–4 days*

**Files involved:**
- `wwwroot/js/tray/trayManager.js` (new file)
- `Pages/Shared/_TastingTray.cshtml` (new partial)
- `wwwroot/js/tray/trayReveal.js` (new file)

**What must change:**
The tray is a `position: fixed, bottom: 0` panel that begins at `transform: translateY(100%)` and transitions to `translateY(0)` on invocation. The tray indicator line (the ambient presence at the bottom of the screen) is a separate, always-present element at 2px height, transitioning from `background: transparent` to `background: var(--annap-amber)` at very low opacity when items exist.

The `TrayManager` maintains tray state in memory (and sessionStorage for refresh resilience). It listens for `annap:drinkAdded` events and updates the tray DOM accordingly.

The swipe-to-open gesture: Implement using `touchstart` / `touchmove` / `touchend` event listeners on a 48px tall transparent touch target at the bottom of the screen. If the user's touch begins in this zone and moves upward by more than 40px, invoke the tray. This prevents accidental invocations.

Left-swipe to remove on tray items: Implement as a transform-based slide with a confirm zone revealed behind the item. The item slides left to reveal a minimal remove indicator. Full swipe (more than 60% of item width) triggers removal with a gentle height-collapse animation.

**Mobile risk — MEDIUM:**
The tray's `backdrop-filter` must not be applied during the opening animation. Add the class containing `backdrop-filter` after the opening transition completes. The `transform: translateY()` animation must use `will-change: transform` declared in advance (on element initialization, not on hover), removed after the animation completes.

---

### Phase 7 — Ambient Idle States
*Priority: Medium | Mobile Risk: Low | Duration: 2–3 days*

**Files involved:**
- `wwwroot/js/ambient/idleManager.js` (new file)
- `Pages/Confirmation.cshtml` or equivalent
- `Pages/Shared/_TablePresence.cshtml` (new partial)

**What must change:**
`IdleManager` monitors `touchstart` events and timestamps. If no interaction occurs for 90 seconds post-confirmation, it triggers the table presence transition. The table presence mode applies a CSS class to the body that triggers style changes across the ambient layers — the wordmark scale increases, the atmospheric lines fade in, the grain increases in opacity fractionally.

The periodic atmospheric line system: a small array of pre-written ambient strings, selected randomly and displayed at 30–45 second intervals via a `setInterval` with jitter (`interval = 30000 + Math.random() * 15000`). Each string fades in over 800ms, holds for 4000ms, and fades out over 600ms.

The radial menu on tap-in-idle: A CSS-animated radial appearance of two options, centered on the tap point. Implemented as a fixed overlay that appears, holds for 6 seconds, and self-dismisses. The 6-second countdown is not shown — it simply disappears, as if the host moved on.

---

### Phase 8 — Motion Polish
*Priority: Medium | Mobile Risk: Medium | Duration: 3–4 days*

**Files involved:**
- `wwwroot/css/annap-motion.css` (new file — the animation stylesheet)
- `wwwroot/js/core/motionPreference.js` (new file)

**What must change:**
Consolidate all animation declarations into `annap-motion.css`. This file is the single source of truth for every transition and keyframe in the application.

Implement `prefers-reduced-motion` throughout: Create a JS class `MotionPreference` that queries the media feature and sets a data attribute on the body — `data-motion="full"` or `data-motion="reduced"`. All animation CSS is gated via this attribute, allowing graceful degradation without removing functional feedback entirely.

```css
[data-motion="reduced"] * {
  animation-duration: 0.01ms !important;
  transition-duration: 0.01ms !important;
}
```

Conduct a full animation audit — every CSS `transition` and `@keyframe` in the codebase — ensuring all animated properties are on the GPU-composited list: `transform`, `opacity`, `filter` (with caution), and `clip-path`. Remove any transitions on `height`, `width`, `top`, `left`, `background-color` (use opacity on overlays instead), or `padding`.

---

### Phase 9 — Performance Optimization
*Priority: Critical (before production) | Mobile Risk: N/A | Duration: 2–3 days*

**Files involved:**
- `wwwroot/js/core/imageLoader.js` (new file)
- All Razor Pages with image content
- `tailwind.config.js`

**What must change:**
Image optimization pipeline: All menu item images must be served at multiple resolutions via `srcset`. Minimum: 400px and 800px wide variants. Format: WebP with JPEG fallback. The blur-up placeholder system must be implemented for all images.

JavaScript bundle strategy: Split the JS into arrival-critical (loads immediately, minimal), menu-critical (loads after arrival, prefetched during mode selection), and ambient (loads lazily, runs only when needed). Do not load the entire application JavaScript on the arrival screen.

Prefetching: When a menu item enters the viewport (Intersection Observer), prefetch its detail content (if served from a separate endpoint) and its full-resolution image. By the time the guest taps, everything is ready.

Touch response latency: Ensure `touch-action: manipulation` is set on all interactive elements to eliminate the 300ms tap delay on mobile browsers. Use `pointer-events: none` aggressively during transitions to prevent unintended interactions during scene changes.

---

## PART 6 — MOBILE PERFORMANCE STRATEGY

### The Luxury Performance Paradox

The fundamental tension: the interactions that create luxury feeling — smooth transitions, ambient motion, layered depth, physical-material feedback — are precisely the interactions most likely to destroy performance on mid-range mobile hardware. The resolution of this tension is not "do less." It is "do the right things the right way."

### The Animation Budget

ANNAP's total simultaneous animation budget on a mid-range device (equivalent to iPhone SE 2nd gen or comparable Android):

- Maximum 3 simultaneously animating properties at any given frame
- Never animate `backdrop-filter` and `transform` simultaneously on the same element
- `opacity` is always free — it is composited without repaint
- `transform` is always free — composited, no layout
- `clip-path` on a single element: safe. On multiple elements simultaneously: dangerous
- `filter: blur()` on a fixed background: safe if the blurred element doesn't change. On scrolling content: catastrophic

### GPU-Safe Transition Architecture

For the tasting tray (the most complex animated surface):

```css
.tasting-tray {
  will-change: transform;    /* declared on page load, not on hover */
  transform: translateY(100%);
  /* backdrop-filter applied ONLY after open animation completes */
  /* via JS: tray.classList.add('tray--open') after transitionend */
}

.tasting-tray.tray--visible {
  transform: translateY(0);
  transition: transform 520ms var(--ease-settle);
}

/* Applied by JS AFTER transition completes: */
.tasting-tray.tray--open {
  backdrop-filter: blur(32px);
  -webkit-backdrop-filter: blur(32px);
}
```

### iPhone Safari Specific

- Never use `position: sticky` inside a `overflow: hidden` or `overflow: scroll` container — it silently fails
- `100vh` is unreliable — always use `window.visualViewport.height` for critical vertical centering
- `touchstart` events must have `{ passive: true }` where possible — except for gesture handlers that may call `preventDefault()`
- WebKit sometimes drops composited layers during scroll on low memory — add `transform: translateZ(0)` to the ambient background and grain overlay to force compositing and prevent them from being dropped

### Instagram/LINE WebView Specific

These browsers inject their own UI chrome, affecting the viewport. Strategy:

- Set `min-height: -webkit-fill-available` alongside `min-height: 100vh`
- Suppress any `overscroll-behavior: contain` on the root — some webviews interpret this as permission to show their own pull-to-refresh
- The arrival choreography should begin only after `window.onload`, not `DOMContentLoaded` — webview resource loading is slower than expected and early animation triggers against incomplete rendering

### Low-End Android Chrome

- Disable the canvas particle system entirely (adventurous mode) below a performance threshold. Detect via: `navigator.hardwareConcurrency <= 2` or `deviceMemory <= 2` (where available)
- Replace `backdrop-filter` with a high-opacity solid background on devices where `CSS.supports('backdrop-filter', 'blur(1px)')` returns false or where the effect noticeably drops frames (use the Performance API to detect first-frame costs)
- Reduce ambient background gradient complexity: on low-end Android, serve a static dark background image instead of the animated gradient

### Scroll Performance

The menu scroll is the highest-risk performance zone. Rules:

- Contain the menu scroll within a dedicated `overflow-y: scroll; -webkit-overflow-scrolling: touch` container, never on the body itself
- All Intersection Observers on menu items must use `threshold: 0` with `rootMargin` offset — this fires fewer callbacks than fractional thresholds
- During scroll momentum (detected by a velocity tracker on `touchmove`), pause all non-essential animations — the ambient background drift, any idle-state behaviors
- Images not within 150% of the viewport height should have their `src` removed and replaced with a placeholder — re-added as they approach the visible zone

---

## PART 7 — WOW MOMENTS

### WOW 01 — The Arrival Bloom

**The interaction:** Guest scans QR. The screen they expect to see — a menu, a list, something functional — does not appear. Instead, warmth. A slow amber bloom expanding from the center of a near-dark screen. Then, from within that warmth, the ANNAP wordmark materializes — impossibly thin, impossibly large, exquisitely calm. Below it, *"You've arrived."* Nothing else.

**Emotional effect:** The guest exhales. They were braced for an interface. They received a welcome. The subconscious recalibration this produces — *this is different, this is something else* — takes approximately 2 seconds to occur and 2 hours to forget.

**Visual behavior:** Radial gradient bloom, `background-size` from 10% to 100% over 1800ms. Wordmark at `font-weight: 100`, `font-size: 52px`, `letter-spacing: 0.18em`, entering via opacity and a 3px vertical translate.

**Motion choreography:** As defined in Scene 01. The still moment at 1600ms is critical — the pause before the welcome line is what creates the feeling of being *addressed* rather than displayed at.

**Implementation complexity:** Low to medium. The choreography is pure CSS + a lightweight JS conductor class. No third-party dependencies. No WebGL. The emotional impact to engineering ratio is the highest in the product.

**Why guests will remember it:** Because no other café, no other QR ordering experience, no other mobile hospitality product has ever made them feel *welcomed* in the first 8 seconds. The novelty of being treated like a guest rather than a user is, in the current digital hospitality landscape, genuinely surprising.

---

### WOW 02 — The Mode Atmosphere Shift

**The interaction:** The guest selects their mode. The entire world of the interface changes emotional temperature — not dramatically, not with a fanfare, but with the slow, certain shift of light quality changing in a physical room as the afternoon becomes evening.

**Emotional effect:** The guest feels *heard*. They made a choice about how they wanted to feel, and the interface *understood that choice* and responded to it atmospherically. This is the experience of being recognized — of a great host having noticed something about you and adjusted accordingly.

**Visual behavior:** The mode atmosphere commit executes across the full session. Background warmth shifts. Typography weight subtly adjusts. The ambient breathing rhythm changes tempo. For the solo guest, the world becomes slightly quieter, slightly more intimate. For the group guest, slightly warmer, more generous. For the adventurous guest, slightly more alive.

**Motion choreography:** The shift takes 600ms via CSS variable transitions, affecting every element on screen simultaneously through inheritance. It feels like the entire room changed, because the entire room *did* change — the CSS root was updated and the cascade propagated.

**Implementation complexity:** Medium. The `AtmosphereManager` class and the CSS variable inheritance system require careful architectural thought. But once implemented, adding new modes or adjusting mode atmospheres is a data change, not an engineering change.

**Why guests will remember it:** The mode selection is the moment ANNAP reveals its genuine emotional intelligence. The guest doesn't just choose a preference — they feel their preference *acknowledged* in a sensory, environmental way. This is not a feature. This is hospitality.

---

### WOW 03 — The Drink Addition Ceremony

**The interaction:** Guest taps "Add to your tasting." A gentle pulse emanates from the tap point. The label cross-fades to "Added with care." A faint ghost of the drink name travels downward toward the bottom of the screen, dissolving as it goes, as if the drink is being carried away to the bar. The tray indicator warms.

**Emotional effect:** The guest smiles. Possibly shows their companion. The action felt *physical* — like placing something, like handing something over. The digital interaction carried the same sensory satisfaction as setting a wine glass on a table.

**Visual behavior:** The tappoint radial pulse, the label cross-fade, the ghost travel, and the tray warm all occur within a 1000ms window, overlapping. The ghost element is a temporary DOM element created and removed programmatically, positioned absolutely over the menu, transitioning from the tap location to the tray indicator coordinates.

**Motion choreography:** As specified in the Motion System. The most complex micro-interaction in the product — requires precise coordinate calculation (the ghost's start and end points change with every interaction, every scroll position).

**Implementation complexity:** High for the ghost travel (coordinate math, DOM creation and removal, performance on rapid successive additions). Medium for the rest of the ceremony. The complexity is worth it.

**Why guests will remember it:** Because it feels like a magic trick. Something physical seemed to happen. The drink *went somewhere*. In a world of instant, invisible, consequence-free digital actions, a confirmation that has *weight* and *travel* and *ceremony* is startlingly moving.

---

### WOW 04 — The Confirmation Bloom

**The interaction:** Guest taps "Send to the bar." Two hundred milliseconds of complete silence — nothing happens. Then: a slow, warm bloom fills the entire screen from the tap point. Everything dissolves into warmth. When the warmth settles, the confirmation scene is present — wordmark centered, warm, the same position as the arrival. *"Your tasting is with us."*

**Emotional effect:** Something felt real. A transaction just occurred, but it felt like a ritual. The bloom — that 1200ms of expanding warmth — is the digital equivalent of a physical gesture of acknowledgment. A nod. A bow. A *"with pleasure."*

**Visual behavior:** CSS `clip-path: circle()` expanding from the tap point to cover the full screen, over 1200ms with a custom slow-to-fast-to-settle easing. The outgoing tray dissolves at 0ms. The incoming confirmation elements arrive at 800ms. The overlap creates a moment where only warmth exists.

**Motion choreography:** The `clip-path: circle(0% at x y)` → `clip-path: circle(150% at x y)` animation is a single CSS transition triggered by JS. It is GPU-composited and frame-rate stable. The confirmation elements underneath begin their own arrival choreography at 800ms — a stagger beginning with the wordmark, then the confirmation line.

**Implementation complexity:** Low to medium. The `clip-path: circle()` technique is well-supported and GPU-composited. The only complexity is capturing the tap coordinates for the bloom origin point.

**Why guests will remember it:** Because it marks the end of their ordering journey with genuine ceremony. Most apps end the transaction with a receipt. ANNAP ends it with a *moment*. The guest will remember that something beautiful happened when they finished ordering — even if they cannot describe exactly what.

---

### WOW 05 — The Table Presence

**The interaction:** The guest's phone rests face-up on the table, in their hand, or simply idle in their lap. The screen, instead of going dark or showing a locked interface, transforms into something beautiful — a slow, warm ambient display. The ANNAP wordmark, very large, very faint, breathing slowly. The atmosphere living and shifting. Occasionally, a line of warm copy drifting through: *"Arriving soon."* The phone becomes a lit object on the table — a piece of atmosphere.

**Emotional effect:** The guest looks down at their phone and finds not a screen but an *environment*. The boundary between the café's physical atmosphere and their digital device has dissolved. The phone is now part of the table setting — an intentional, beautiful object contributing to the ambient experience rather than interrupting it.

**Visual behavior:** The wordmark at 120% of its arrival size, opacity 0.15, centered. The ambient background performing its full sinusoidal drift. The grain texture slightly amplified. Occasional copy lines drifting in and out at very low opacity. The screen's own brightness, if controllable via the Screen Brightness API (available in some mobile contexts), reduced to 30%.

**Motion choreography:** The transition into table presence is very slow — 1800ms — so gradual that the guest barely notices it happening. It is the most ambient motion in the product. The periodic copy lines use the same timing as a breathing cycle — appearing as if the room itself is murmuring.

**Implementation complexity:** Low. The idle manager and transition are simple timing-based class changes. The visual system is already in place from the arrival scene — this is largely a reuse of the ambient layer at different scale and opacity values.

**Why guests will remember it:** Because their phone was *beautiful* while they waited. Not dark, not locked, not showing a timer — *beautiful*. When their drinks arrive and they pick up their phone, they will have spent the last several minutes in the presence of something warm and alive. This is the experience of ANNAP extending beyond the digital interface into the physical moment of the visit. This is the product becoming, briefly, invisible — and in that invisibility, becoming genuinely experiential.

---

*ANNAP Frontend Refactor Blueprint — Complete.*
*For conversion to Cursor implementation prompts: each Phase in Part 5 converts to a discrete Cursor session with its own context, file scope, and implementation goal. Begin with Phase 1. Do not proceed to Phase 2 until the foundation CSS variable system is stable and verified across iPhone Safari, Chrome Android, and at least one in-app browser.*

*The vision is complete. The craft begins now.*