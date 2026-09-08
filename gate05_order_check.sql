SELECT
  'total_orders' AS metric,
  COUNT(*)::text AS value
FROM orders
UNION ALL
SELECT
  'submitted_orders',
  COUNT(*)::text
FROM orders
WHERE "Status" = 'Submitted';
