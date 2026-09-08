SELECT
  c."Name",
  COUNT(i."Id") AS items
FROM menu_categories c
LEFT JOIN menu_items i ON i."CategoryId" = c."Id"
GROUP BY c."Id", c."Name"
ORDER BY c."Name";
