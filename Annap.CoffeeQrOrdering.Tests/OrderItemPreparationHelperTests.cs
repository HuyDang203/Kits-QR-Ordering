using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Web.Internal;

namespace Annap.CoffeeQrOrdering.Tests;

public sealed class OrderItemPreparationHelperTests
{
    [Fact]
    public void SetPreparedQuantity_clamps_to_quantity_range()
    {
        var item = new OrderItem { Quantity = 3, PreparedQuantity = 0 };
        var now = DateTimeOffset.UtcNow;
        var accountId = Guid.NewGuid();

        OrderItemPreparationHelper.SetPreparedQuantity(item, -1, "barista", accountId, now);
        Assert.Equal(0, item.PreparedQuantity);
        Assert.Null(item.PreparedAtUtc);

        OrderItemPreparationHelper.SetPreparedQuantity(item, 5, "barista", accountId, now);
        Assert.Equal(3, item.PreparedQuantity);
        Assert.NotNull(item.PreparedAtUtc);
        Assert.Equal("barista", item.PreparedBy);
        Assert.Equal(accountId, item.PreparedByAccountId);
    }

    [Fact]
    public void SetPreparedQuantity_idempotent_does_not_overwrite_attribution()
    {
        var item = new OrderItem { Quantity = 2, PreparedQuantity = 2, PreparedBy = "A", PreparedByAccountId = Guid.NewGuid() };
        var now = DateTimeOffset.UtcNow;

        var changed = OrderItemPreparationHelper.SetPreparedQuantity(item, 2, "B", Guid.NewGuid(), now);

        Assert.False(changed);
        Assert.Equal("A", item.PreparedBy);
    }

    [Fact]
    public void IsOrderFullyPrepared_requires_all_lines_complete()
    {
        var order = new Order
        {
            Items =
            [
                new OrderItem { Quantity = 2, PreparedQuantity = 2 },
                new OrderItem { Quantity = 1, PreparedQuantity = 0 }
            ]
        };
        Assert.False(OrderItemPreparationHelper.IsOrderFullyPrepared(order));

        order.Items[1].PreparedQuantity = 1;
        Assert.True(OrderItemPreparationHelper.IsOrderFullyPrepared(order));
    }

    [Fact]
    public void CountProgress_sums_cup_quantities()
    {
        var order = new Order
        {
            Items =
            [
                new OrderItem { Quantity = 2, PreparedQuantity = 1 },
                new OrderItem { Quantity = 1, PreparedQuantity = 1 }
            ]
        };
        var (done, total) = OrderItemPreparationHelper.CountProgress(order);
        Assert.Equal(2, done);
        Assert.Equal(3, total);
    }
}
