/**
 * Functional register — prices, categories, signal micro-type.
 */
export default function FunctionalLabel({ as: Tag = "span", className = "", style, children, ...rest }) {
  return (
    <Tag
      className={className}
      style={{
        fontFamily: "var(--font-functional)",
        fontWeight: 500,
        fontSize: "var(--text-functional)",
        lineHeight: "var(--leading-functional)",
        letterSpacing: "var(--tracking-functional)",
        textTransform: "uppercase",
        color: "var(--ink-tertiary)",
        ...style,
      }}
      {...rest}
    >
      {children}
    </Tag>
  );
}
