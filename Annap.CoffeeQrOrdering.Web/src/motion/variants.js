/**
 * ANNAP — motion variant definitions (plain objects).
 * Intended for Framer Motion `variants={...}` when that dependency is added.
 * Phase 0: do not wire broadly; import only where experiments run.
 *
 * Rules: prefer transform + opacity only; no random durations — use token-aligned seconds.
 */

export const paperReveal = {
  hidden: { opacity: 0, y: 16 },
  visible: {
    opacity: 1,
    y: 0,
    transition: { duration: 0.65, ease: [0.16, 1, 0.3, 1] },
  },
  exit: {
    opacity: 0,
    y: -8,
    transition: { duration: 0.45, ease: [0.7, 0, 0.84, 0] },
  },
};

export const writingReveal = {
  hidden: { clipPath: "inset(0 100% 0 0)" },
  visible: {
    clipPath: "inset(0 0% 0 0)",
    transition: { duration: 0.6, ease: [0.16, 1, 0.3, 1] },
  },
};

export const pageScene = {
  hidden: { opacity: 0 },
  visible: {
    opacity: 1,
    transition: { duration: 0.65, ease: [0.16, 1, 0.3, 1] },
  },
  exit: {
    opacity: 0,
    transition: { duration: 0.35, ease: [0.7, 0, 0.84, 0] },
  },
};

export const trayRise = {
  hidden: { y: "100%" },
  visible: {
    y: 0,
    transition: { duration: 0.52, ease: [0.34, 1.56, 0.64, 1] },
  },
  exit: {
    y: "100%",
    transition: { duration: 0.38, ease: [0.7, 0, 0.84, 0] },
  },
};

export const letterChild = {
  hidden: { opacity: 0, y: 10 },
  visible: {
    opacity: 1,
    y: 0,
    transition: { duration: 0.45, ease: [0.16, 1, 0.3, 1] },
  },
};

export const letterEntryStagger = {
  hidden: {},
  visible: {
    transition: { staggerChildren: 0.08 },
  },
};

export const motionVariants = {
  paperReveal,
  writingReveal,
  pageScene,
  trayRise,
  letterChild,
  letterEntryStagger,
};
