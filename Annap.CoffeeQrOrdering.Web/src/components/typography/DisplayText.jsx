/**
 * Display register — editorial / whisper (Playfair Display italic).
 * Requires src/styles/tokens.css loaded on the page.
 */
export default function DisplayText({ as: Tag = "span", className = "", style, children, ...rest }) {
  return (
    <Tag
      className={className}
      style={{
        fontFamily: "var(--font-display)",
        fontWeight: 300,
        fontStyle: "italic",
        fontSize: "var(--text-display)",
        lineHeight: "var(--leading-display)",
        letterSpacing: "var(--tracking-display)",
        color: "var(--ink-primary)",
        ...style,
      }}
      {...rest}
    >
      {children}
    </Tag>
  );
}
