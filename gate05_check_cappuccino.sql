SELECT "Id"::text, "Name", "CatalogKey", "IsArchived", "IsAvailable"
FROM menu_items
WHERE "CatalogKey" = 'kits-cafe-cappuccino';
