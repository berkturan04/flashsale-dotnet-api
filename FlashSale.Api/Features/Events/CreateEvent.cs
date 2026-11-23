using FlashSale.Api.Database;
using MediatR;

namespace FlashSale.Api.Features.Events;

public static class CreateEvent
{
    // 1. Command (Yazma İsteği)
    public record Command(
        string Title,
        int TotalStock,
        decimal Price,
        DateTime StartsAtUtc) : IRequest<Response>;

    // 2. Response (Dışarıya Dönen DTO)
    public record Response(
        Guid Id,
        string Title,
        int AvailableStock,
        decimal Price,
        DateTime StartsAtUtc);

    // 3. Handler (Veritabanına Yazma Mantığı)
    public sealed class Handler(AppDbContext db) : IRequestHandler<Command, Response>
    {
        public async Task<Response> Handle(Command request, CancellationToken ct)
        {
            var startsAtUtc = request.StartsAtUtc.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(request.StartsAtUtc, DateTimeKind.Utc)
                : request.StartsAtUtc.ToUniversalTime();

            var newEvent = new Event
            {
                Title = request.Title,
                TotalStock = request.TotalStock,
                AvailableStock = request.TotalStock,
                Price = request.Price,
                StartsAtUtc = startsAtUtc
            };

            db.Events.Add(newEvent);
            await db.SaveChangesAsync(ct);

            return new Response(
                newEvent.Id,
                newEvent.Title,
                newEvent.AvailableStock,
                newEvent.Price,
                newEvent.StartsAtUtc);
        }
    }

    // 4. Minimal API Uç Noktası
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/events", async (Command command, ISender sender, CancellationToken ct) =>
        {
            var response = await sender.Send(command, ct);
            return Results.Created($"/api/events/{response.Id}", response);
        })
        .WithTags("Events");
    }
}