using BurgerIAM.TestUtilities;
using FeedbackService.Data;
using FeedbackService.Services;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using ProtoCommon = BurgerIAM.Protos.Common;
using ProtoFeedback = BurgerIAM.Protos.Feedback;

namespace FeedbackService.Tests;

public class FeedbackGrpcServiceTests
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static FeedbackGrpcService CreateService(AppDbContext db)
    {
        var eventBus = new InMemoryEventBus();
        return new FeedbackGrpcService(db, eventBus);
    }

    [Fact]
    public async Task SubmitFeedback_ValidRequest_ReturnsFeedbackId()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        var response = await service.SubmitFeedback(new ProtoFeedback.SubmitFeedbackRequest
        {
            OrderId = "order-1",
            CustomerId = "customer-1",
            Rating = 5,
            Comment = "Great food!"
        }, new MockServerCallContext());
        Assert.NotEmpty(response.FeedbackId);
        Assert.True(string.IsNullOrWhiteSpace(response.Error));
    }

    [Fact]
    public async Task SubmitFeedback_DuplicateOrder_ReturnsError()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        db.FeedbackEntries.Add(new FeedbackEntity { OrderId = "order-1", CustomerId = "customer-1", Rating = 5 });
        await db.SaveChangesAsync();
        var response = await service.SubmitFeedback(new ProtoFeedback.SubmitFeedbackRequest
        {
            OrderId = "order-1",
            CustomerId = "customer-1",
            Rating = 4,
            Comment = "Still good"
        }, new MockServerCallContext());
        Assert.NotEmpty(response.Error);
    }

    [Fact]
    public async Task SubmitFeedback_InvalidRating_ReturnsError()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        var response = await service.SubmitFeedback(new ProtoFeedback.SubmitFeedbackRequest
        {
            OrderId = "order-1",
            CustomerId = "customer-1",
            Rating = 6,
            Comment = "Too high"
        }, new MockServerCallContext());
        Assert.NotEmpty(response.Error);
    }

    [Fact]
    public async Task GetOrderFeedback_Existing_ReturnsFeedback()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        db.FeedbackEntries.Add(new FeedbackEntity { OrderId = "order-1", CustomerId = "customer-1", Rating = 4, Comment = "Yummy" });
        await db.SaveChangesAsync();
        var response = await service.GetOrderFeedback(new ProtoFeedback.GetOrderFeedbackRequest { OrderId = "order-1" }, new MockServerCallContext());
        Assert.Equal("order-1", response.OrderId);
        Assert.Equal(4, response.Rating);
        Assert.Equal("Yummy", response.Comment);
    }

    [Fact]
    public async Task GetOrderFeedback_NonExistent_ThrowsNotFound()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            service.GetOrderFeedback(new ProtoFeedback.GetOrderFeedbackRequest { OrderId = "nonexistent" }, new MockServerCallContext()));
        Assert.Equal(StatusCode.NotFound, ex.StatusCode);
    }

    [Fact]
    public async Task GetAverageRating_EmptyDb_ReturnsZero()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        var response = await service.GetAverageRating(new ProtoFeedback.GetAverageRatingRequest(), new MockServerCallContext());
        Assert.Equal(0, response.AverageRating);
        Assert.Equal(0, response.TotalReviews);
    }

    [Fact]
    public async Task GetAverageRating_WithRatings_ReturnsAverage()
    {
        var db = CreateDbContext();
        var service = CreateService(db);
        db.FeedbackEntries.Add(new FeedbackEntity { OrderId = "order-1", CustomerId = "c1", Rating = 5 });
        db.FeedbackEntries.Add(new FeedbackEntity { OrderId = "order-2", CustomerId = "c2", Rating = 3 });
        db.FeedbackEntries.Add(new FeedbackEntity { OrderId = "order-3", CustomerId = "c3", Rating = 4 });
        await db.SaveChangesAsync();
        var response = await service.GetAverageRating(new ProtoFeedback.GetAverageRatingRequest(), new MockServerCallContext());
        Assert.Equal(4.0, response.AverageRating);
        Assert.Equal(3, response.TotalReviews);
    }
}
