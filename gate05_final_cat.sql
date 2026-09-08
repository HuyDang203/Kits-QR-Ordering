SELECT
  c."Name",
  COUNT(m."Id") FILTER (WHERE m."IsArchived" = false AND m."IsAvailable" = true) AS active,
  COUNT(m."Id") FILTER (WHERE m."IsArchived" = true) AS archived
FROM menu_categories c
LEFT JOIN menu_items m ON m."CategoryId" = c."Id"
GROUP BY c."Id", c."Name"
ORDER BY c."SortOrder", c."Name";
