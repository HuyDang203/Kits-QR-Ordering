INSERT INTO "menu_items" ("Id", "CategoryId", "Name", "CatalogKey", "ItemType", "Description", "Price", "DisplaySortOrder", "IsAvailable", "IsArchived", "IsFeatured", "IsSignature", "CreatedAtUtc")
SELECT
    gen_random_uuid() AS "Id",
    '47cdbfd6-da61-4bbf-94a5-d4ff7ab3d167'::uuid AS "CategoryId",
    'Cappuccino' AS "Name",
    'kits-cafe-cappuccino' AS "CatalogKey",
    'hot' AS "ItemType",
    'Espresso, sữa tươi hấp, bọt mịn' AS "Description",
    45000.00 AS "Price",
    11 AS "DisplaySortOrder",
    true AS "IsAvailable",
    false AS "IsArchived",
    false AS "IsFeatured",
    false AS "IsSignature",
    NOW() AS "CreatedAtUtc"
WHERE NOT EXISTS (
    SELECT 1 FROM "menu_items" WHERE "CatalogKey" = 'kits-cafe-cappuccino'
);
