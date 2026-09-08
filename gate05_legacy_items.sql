SELECT
  m."Name",
  m."IsArchived",
  m."IsAvailable",
  c."Name" AS category,
  m."CatalogKey"
FROM menu_items m
JOIN menu_categories c ON c."Id" = m."CategoryId"
WHERE m."CatalogKey" IS NULL OR m."CatalogKey" NOT LIKE 'kits-%'
ORDER BY c."SortOrder", m."Name";
