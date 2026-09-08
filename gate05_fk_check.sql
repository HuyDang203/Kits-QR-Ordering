SELECT conname, conrelid::regclass AS table_name, confrelid::regclass AS ref_table,
       pg_get_constraintdef(oid) AS definition
FROM pg_constraint
WHERE conrelid = 'order_items'::regclass AND contype = 'f';
