using BurgerIAM.Shared.Enums;

namespace BurgerIAM.Shared.Tests;

public class EnumTests
{
    [Fact]
    public void OrderStatus_HasExpectedValues()
    {
        Assert.Equal(0, (int)OrderStatus.Pending);
        Assert.Equal(1, (int)OrderStatus.Confirmed);
        Assert.Equal(2, (int)OrderStatus.Paid);
        Assert.Equal(3, (int)OrderStatus.Preparing);
        Assert.Equal(4, (int)OrderStatus.Ready);
        Assert.Equal(5, (int)OrderStatus.OutForDelivery);
        Assert.Equal(6, (int)OrderStatus.Delivered);
        Assert.Equal(7, (int)OrderStatus.Cancelled);
        Assert.Equal(8, (int)OrderStatus.Refunded);
    }

    [Fact]
    public void PaymentStatus_HasExpectedValues()
    {
        Assert.Equal(0, (int)PaymentStatus.Pending);
        Assert.Equal(1, (int)PaymentStatus.Processing);
        Assert.Equal(2, (int)PaymentStatus.Confirmed);
        Assert.Equal(3, (int)PaymentStatus.Failed);
        Assert.Equal(4, (int)PaymentStatus.Refunded);
    }

    [Fact]
    public void DeliveryStatus_HasExpectedValues()
    {
        Assert.Equal(0, (int)DeliveryStatus.Unassigned);
        Assert.Equal(1, (int)DeliveryStatus.Assigned);
        Assert.Equal(2, (int)DeliveryStatus.PickedUp);
        Assert.Equal(3, (int)DeliveryStatus.InTransit);
        Assert.Equal(4, (int)DeliveryStatus.Delivered);
        Assert.Equal(5, (int)DeliveryStatus.Failed);
    }
}
