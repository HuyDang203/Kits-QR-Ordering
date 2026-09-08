SELECT "Id"::text, "Name", "SortOrder"
FROM menu_categories
WHERE "Name" IN ('CAFE', 'Specialty Coffee', 'Good Things', 'Espresso', 'Tea', 'Smoothie', 'Juice', 'Cold Brew', 'Vietnamese Coffee', 'Signature')
ORDER BY "SortOrder", "Name";
