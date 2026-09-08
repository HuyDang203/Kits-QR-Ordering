SELECT count(*) FILTER (WHERE "IsAvailable" AND NOT "IsArchived") AS available,
       count(*) AS total,
       count(*) FILTER (WHERE NOT "IsAvailable" OR "IsArchived") AS hidden
FROM "MenuItems";
