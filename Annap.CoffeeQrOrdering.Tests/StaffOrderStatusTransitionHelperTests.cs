using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Web.Internal;

namespace Annap.CoffeeQrOrdering.Tests;

public sealed class StaffOrderStatusTransitionHelperTests
{
    [Theory]
    [InlineData(OrderStatus.Submitted, OrderStatus.Paid, true)]
    [InlineData(OrderStatus.Paid, OrderStatus.InProgress, true)]
    [InlineData(OrderStatus.Submitted, OrderStatus.InProgress, true)]
    [InlineData(OrderStatus.InProgress, OrderStatus.FinishingTouches, true)]
    [InlineData(OrderStatus.FinishingTouches, OrderStatus.Ready, true)]
    [InlineData(OrderStatus.Ready, OrderStatus.Completed, true)]
    [InlineData(OrderStatus.Completed, OrderStatus.InProgress, false)]
    [InlineData(OrderStatus.Ready, OrderStatus.Submitted, false)]
    [InlineData(OrderStatus.InProgress, OrderStatus.Submitted, false)]
    public void IsValidTransition_enforces_forward_only_cafe_workflow(
        OrderStatus current,
        OrderStatus next,
        bool expected)
    {
        Assert.Equal(expected, StaffOrderStatusTransitionHelper.IsValidTransition(current, next));
    }

    [Fact]
    public void IsValidTransition_allows_same_status_noop()
    {
        Assert.True(StaffOrderStatusTransitionHelper.IsValidTransition(OrderStatus.Ready, OrderStatus.Ready));
        Assert.True(StaffOrderStatusTransitionHelper.IsValidTransition(OrderStatus.Completed, OrderStatus.Completed));
    }

    [Fact]
    public void IsValidTransition_blocks_changes_from_cancelled()
    {
        Assert.False(StaffOrderStatusTransitionHelper.IsValidTransition(OrderStatus.Cancelled, OrderStatus.Submitted));
        Assert.False(StaffOrderStatusTransitionHelper.IsValidTransition(OrderStatus.Cancelled, OrderStatus.InProgress));
    }

    [Fact]
    public void ParseStaffStatus_rejects_unknown_value()
    {
        var ex = Assert.Throws<ArgumentException>(() => StaffOrderStatusHelper.ParseStaffStatus("not-a-real-status"));
        Assert.Contains("Unknown", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("submitted", OrderStatus.Submitted)]
    [InlineData("pending", OrderStatus.Submitted)]
    [InlineData("paid", OrderStatus.Paid)]
    [InlineData("preparing", OrderStatus.InProgress)]
    [InlineData("finishing", OrderStatus.FinishingTouches)]
    [InlineData("ready", OrderStatus.Ready)]
    [InlineData("served", OrderStatus.Completed)]
    public void ParseStaffStatus_maps_known_staff_labels(string label, OrderStatus expected)
    {
        Assert.Equal(expected, StaffOrderStatusHelper.ParseStaffStatus(label));
    }
}
