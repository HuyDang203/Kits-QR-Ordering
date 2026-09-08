SELECT "Id"::text, "Name", "IsArchived", "IsAvailable", "CategoryId"::text FROM menu_items WHERE "Id"::text ILIKE '%c6413aec%';
SELECT "Name", "SortOrder" FROM menu_categories ORDER BY "Name";
