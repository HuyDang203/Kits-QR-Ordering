/**
 * Narrative register — conversation body (EB Garamond).
 */
export default function NarrativeText({ as: Tag = "p", className = "", style, children, ...rest }) {
  return (
    <Tag
      className={className}
      style={{
        fontFamily: "var(--font-narrative)",
        fontWeight: 400,
        fontStyle: "normal",
        fontSize: "var(--text-narrative)",
        lineHeight: "var(--leading-narrative)",
        letterSpacing: "var(--tracking-narrative)",
        color: "var(--ink-secondary)",
        maxWidth: "32ch",
        ...style,
      }}
      {...rest}
    >
      {children}
    </Tag>
  );
}
