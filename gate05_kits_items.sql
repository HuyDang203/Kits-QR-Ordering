SELECT
  m."Name",
  m."CatalogKey",
  c."Name" AS category
FROM menu_items m
JOIN menu_categories c ON c."Id" = m."CategoryId"
WHERE m."CatalogKey" IS NOT NULL AND m."CatalogKey" LIKE 'kits-%'
ORDER BY c."SortOrder", m."DisplaySortOrder";
