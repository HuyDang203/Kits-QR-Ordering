INSERT INTO menu_items (
    "Id", "CategoryId", "Name", "CatalogKey", "ItemType",
    "Description", "Price", "DisplaySortOrder",
    "IsAvailable", "IsArchived", "IsFeatured", "IsSignature",
    "CreatedAtUtc", "ImageUrl", "DetailPosterImagePath"
) VALUES (
    gen_random_uuid(),
    '47cdbfd6-da61-4bbf-94a5-d4ff7ab3d167'::uuid,
    'Cappuccino',
    'kits-cafe-cappuccino',
    'hot',
    'Espresso, sữa tươi hấp, bọt mịn',
    45000,
    11,
    true,
    false,
    false,
    false,
    NOW(),
    NULL,
    NULL
);
