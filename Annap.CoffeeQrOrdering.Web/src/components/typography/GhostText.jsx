/**
 * Ghost register — ambient UI, watermarks, metadata at rest.
 */
export default function GhostText({ as: Tag = "span", className = "", style, children, ...rest }) {
  return (
    <Tag
      className={className}
      style={{
        fontFamily: "var(--font-functional)",
        fontWeight: 400,
        fontSize: "var(--text-ghost)",
        lineHeight: "var(--leading-functional)",
        letterSpacing: "var(--tracking-ghost)",
        textTransform: "uppercase",
        color: "var(--ink-ghost)",
        ...style,
      }}
      {...rest}
    >
      {children}
    </Tag>
  );
}
