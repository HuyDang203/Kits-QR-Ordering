SELECT
  m."Name" AS item_name,
  c."Name" AS category,
  COUNT(oi."Id") AS order_lines
FROM menu_items m
JOIN menu_categories c ON c."Id" = m."CategoryId"
LEFT JOIN order_items oi ON oi."MenuItemId" = m."Id"
WHERE m."CatalogKey" IS NULL OR m."CatalogKey" NOT LIKE 'kits-%'
GROUP BY m."Name", c."Name"
ORDER BY order_lines DESC, c."Name", m."Name";
