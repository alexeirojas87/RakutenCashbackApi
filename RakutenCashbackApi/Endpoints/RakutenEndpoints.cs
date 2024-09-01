using RakutenCashbackApi.Models;
using System.Collections.Concurrent;

namespace RakutenCashbackApi.Endpoints
{
    public static class RakutenEndpoints
    {
        public static void MapRakutenEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapGet("api/cashbacks", handler: static () => Results.Ok(DtoStores.OrderByDescending(s => s.CashBackAmount)))
               .WithName("GetCashbacksStore")
               .WithOpenApi();
        }
        public static ConcurrentBag<StoreDto> DtoStores { get; set; } = [];
    }
}
