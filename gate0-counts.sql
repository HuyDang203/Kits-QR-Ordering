SELECT c."Name", c."SortOrder", count(i."Id") AS item_count
FROM "MenuCategories" c
LEFT JOIN "MenuItems" i ON i."CategoryId" = c."Id"
GROUP BY c."Id", c."Name", c."SortOrder"
ORDER BY c."SortOrder";
