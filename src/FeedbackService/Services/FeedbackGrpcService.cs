using BurgerIAM.EventBus;
using BurgerIAM.Shared.Events;
using FeedbackService.Data;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using ProtoCommon = BurgerIAM.Protos.Common;
using ProtoFeedback = BurgerIAM.Protos.Feedback;

namespace FeedbackService.Services;

public sealed class FeedbackGrpcService : ProtoFeedback.FeedbackService.FeedbackServiceBase
{
    private readonly AppDbContext _db;
    private readonly IEventBus _eventBus;

    public FeedbackGrpcService(AppDbContext db, IEventBus eventBus)
    {
        _db = db;
        _eventBus = eventBus;
    }

    public override async Task<ProtoFeedback.SubmitFeedbackResponse> SubmitFeedback(ProtoFeedback.SubmitFeedbackRequest request, ServerCallContext context)
    {
        var existing = await _db.FeedbackEntries.AnyAsync(f => f.OrderId == request.OrderId, context.CancellationToken);
        if (existing)
            return new ProtoFeedback.SubmitFeedbackResponse { Error = "Feedback already submitted for this order" };

        if (request.Rating < 1 || request.Rating > 5)
            return new ProtoFeedback.SubmitFeedbackResponse { Error = "Rating must be between 1 and 5" };

        var feedback = new FeedbackEntity
        {
            OrderId = request.OrderId,
            CustomerId = request.CustomerId,
            Rating = request.Rating,
            Comment = request.Comment
        };

        _db.FeedbackEntries.Add(feedback);
        await _db.SaveChangesAsync(context.CancellationToken);

        await _eventBus.PublishAsync(new FeedbackSubmittedEvent
        {
            OrderId = feedback.OrderId,
            CustomerId = feedback.CustomerId,
            Rating = feedback.Rating,
            Comment = feedback.Comment
        }, context.CancellationToken);

        return new ProtoFeedback.SubmitFeedbackResponse { FeedbackId = feedback.Id };
    }

    public override async Task<ProtoFeedback.Feedback> GetOrderFeedback(ProtoFeedback.GetOrderFeedbackRequest request, ServerCallContext context)
    {
        var feedback = await _db.FeedbackEntries.FirstOrDefaultAsync(f => f.OrderId == request.OrderId, context.CancellationToken);

        if (feedback is null)
            throw new RpcException(new Status(StatusCode.NotFound, $"Feedback for order {request.OrderId} not found"));

        return MapToProto(feedback);
    }

    public override async Task<ProtoFeedback.AverageRatingResponse> GetAverageRating(ProtoFeedback.GetAverageRatingRequest request, ServerCallContext context)
    {
        var ratings = await _db.FeedbackEntries.ToListAsync(context.CancellationToken);

        if (ratings.Count == 0)
            return new ProtoFeedback.AverageRatingResponse { AverageRating = 0, TotalReviews = 0 };

        return new ProtoFeedback.AverageRatingResponse
        {
            AverageRating = ratings.Average(f => f.Rating),
            TotalReviews = ratings.Count
        };
    }

    private static ProtoFeedback.Feedback MapToProto(FeedbackEntity entity)
    {
        return new ProtoFeedback.Feedback
        {
            Id = entity.Id,
            OrderId = entity.OrderId,
            CustomerId = entity.CustomerId,
            Rating = entity.Rating,
            Comment = entity.Comment ?? string.Empty,
            CreatedAt = entity.CreatedAt.ToString("O")
        };
    }
}
