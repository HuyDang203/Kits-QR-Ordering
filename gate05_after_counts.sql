SELECT
  'total_categories' AS metric, COUNT(*)::text AS value FROM menu_categories
UNION ALL SELECT 'total_items', COUNT(*)::text FROM menu_items
UNION ALL SELECT 'active_items', COUNT(*)::text FROM menu_items WHERE "IsArchived" = false AND "IsAvailable" = true
UNION ALL SELECT 'archived_items', COUNT(*)::text FROM menu_items WHERE "IsArchived" = true
UNION ALL SELECT 'kits_items', COUNT(*)::text FROM menu_items WHERE "CatalogKey" LIKE 'kits-%';
