SELECT "Id"::text, "Name", "CatalogKey", "IsArchived", "IsAvailable"
FROM menu_items
WHERE "CatalogKey" LIKE 'kits-%'
ORDER BY "Name";
