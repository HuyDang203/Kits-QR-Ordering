-- Check all Espresso-category items
SELECT "Name", "CatalogKey" FROM menu_items WHERE "CategoryId" IN (SELECT "Id" FROM menu_categories WHERE "Name" = 'Espresso');

-- Check kits items grouped by category
SELECT c."Name" AS category, COUNT(m."Id") AS kits_count
FROM menu_items m
JOIN menu_categories c ON c."Id" = m."CategoryId"
WHERE m."CatalogKey" LIKE 'kits-%'
GROUP BY c."Name" ORDER BY c."SortOrder";

-- Count total kits items
SELECT COUNT(*) AS total FROM menu_items WHERE "CatalogKey" LIKE 'kits-%';

-- Check if "Bạc" items exist in Vietnamese Coffee category
SELECT "Name", "CatalogKey" FROM menu_items WHERE "Name" ILIKE '%bạc%';

-- Check for "Cappuccino" or "Capuchino"  
SELECT "Name", "CatalogKey", "CategoryId" FROM menu_items WHERE "Name" ILIKE '%appuccin%' OR "Name" ILIKE '%apuchino%';

-- Check if there are any kits items in the Vietnamese Coffee category
SELECT m."Name", m."CatalogKey", c."Name" AS category
FROM menu_items m
JOIN menu_categories c ON c."Id" = m."CategoryId"
WHERE c."Name" = 'Vietnamese Coffee';
