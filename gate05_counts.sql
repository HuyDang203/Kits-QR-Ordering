SELECT 'total_categories' AS metric, COUNT(*)::text AS value FROM menu_categories
UNION ALL SELECT 'total_items', COUNT(*)::text FROM menu_items
UNION ALL SELECT 'archived_items', COUNT(*)::text FROM menu_items WHERE "IsArchived" = true
UNION ALL SELECT 'available_items', COUNT(*)::text FROM menu_items WHERE "IsArchived" = false AND "IsAvailable" = true
UNION ALL SELECT 'unavailable_active_items', COUNT(*)::text FROM menu_items WHERE "IsArchived" = false AND "IsAvailable" = false
UNION ALL SELECT 'categories_with_active_items', COUNT(DISTINCT "CategoryId")::text FROM menu_items WHERE "IsArchived" = false AND "IsAvailable" = true
UNION ALL SELECT 'empty_categories', COUNT(*)::text FROM menu_categories c WHERE NOT EXISTS (SELECT 1 FROM menu_items i WHERE i."CategoryId" = c."Id" AND i."IsArchived" = false AND i."IsAvailable" = true);
