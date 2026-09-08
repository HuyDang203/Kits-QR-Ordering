SELECT COUNT(*) AS total_order_lines FROM order_items;
SELECT COUNT(DISTINCT "MenuItemId") AS unique_items_ordered FROM order_items;
