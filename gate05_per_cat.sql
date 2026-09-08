SELECT
  c."Name" AS category,
  c."SortOrder",
  COUNT(i."Id") FILTER (WHERE i."IsArchived" = false AND i."IsAvailable" = true) AS available,
  COUNT(i."Id") FILTER (WHERE i."IsArchived" = true) AS archived,
  COUNT(i."Id") AS total
FROM menu_categories c
LEFT JOIN menu_items i ON i."CategoryId" = c."Id"
GROUP BY c."Id", c."Name", c."SortOrder"
ORDER BY c."SortOrder", c."Name";
