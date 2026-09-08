using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Web.Internal;

namespace Annap.CoffeeQrOrdering.Tests;

public sealed class LegacyStaffStatusPatchHelperTests
{
    [Theory]
    [InlineData(OrderStatus.Submitted, OrderStatus.Completed)]
    [InlineData(OrderStatus.Submitted, OrderStatus.InProgress)]
    [InlineData(OrderStatus.Submitted, OrderStatus.Paid)]
    [InlineData(OrderStatus.Paid, OrderStatus.Completed)]
    [InlineData(OrderStatus.InProgress, OrderStatus.Completed)]
    public void Legacy_patch_blocks_payment_workflow_bypass(OrderStatus current, OrderStatus next)
    {
        Assert.False(LegacyStaffStatusPatchHelper.IsAllowed(current, next));
    }

    [Fact]
    public void Legacy_patch_allows_admin_prep_tweaks_among_kitchen_states()
    {
        Assert.True(LegacyStaffStatusPatchHelper.IsAllowed(OrderStatus.InProgress, OrderStatus.Ready));
        Assert.True(LegacyStaffStatusPatchHelper.IsAllowed(OrderStatus.Paid, OrderStatus.InProgress));
    }

    [Fact]
    public void Legacy_patch_allows_noop()
    {
        Assert.True(LegacyStaffStatusPatchHelper.IsAllowed(OrderStatus.Ready, OrderStatus.Ready));
    }
}
