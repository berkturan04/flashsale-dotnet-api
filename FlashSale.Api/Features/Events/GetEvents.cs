using FlashSale.Api.Database;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FlashSale.Api.Features.Events;

public static class GetEvents
{
    // 1. Query (Okuma İsteği)
    public record Query(bool OnlyUpcoming = false) : IRequest<List<Response>>;

    // 2. Response (Sadece İstemcinin İhtiyacı Olan Sütunlar)
    public record Response(
        Guid Id,
        string Title,
        int AvailableStock,
        decimal Price,
        DateTime StartsAtUtc);

    // 3. Handler (%20 ODAK: AsNoTracking + Select Projeksiyonu + İndeks Kullanımı)
    public sealed class Handler(AppDbContext db) : IRequestHandler<Query, List<Response>>
    {
        public async Task<List<Response>> Handle(Query request, CancellationToken ct)
        {
            var query = db.Events
                .AsNoTracking(); // Change Tracker'ı kapatır: RAM ve CPU maliyeti düşer

            if (request.OnlyUpcoming)
            {
                var now = DateTime.UtcNow;
                // 2. Günde kurduğumuz IX_Events_StartsAtUtc indeksini doğrudan tetikler
                query = query.Where(e => e.StartsAtUtc >= now);
            }

            return await query
                .OrderBy(e => e.StartsAtUtc)
                // Tüm Entity'yi belleğe çekmek yerine sadece 5 kolonu SQL seviyesinde seçer
                .Select(e => new Response(
                    e.Id,
                    e.Title,
                    e.AvailableStock,
                    e.Price,
                    e.StartsAtUtc))
                .ToListAsync(ct);
        }
    }

    // 4. Minimal API Uç Noktası
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/events", async (bool? onlyUpcoming, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new Query(onlyUpcoming ?? false), ct);
            return Results.Ok(result);
        })
        .WithTags("Events");
    }
}