namespace UltraSol.Modules.Ordering.Application.Checkout;

public interface ICatalogCheckoutClient
{
    Task<CatalogItem[]> GetAsync(Guid[] productItemIds, CancellationToken ct);
}
public interface IPricingQuoteClient
{
    Task<PriceQuote> QuoteAsync(QuoteRequest request, CancellationToken ct);
}
public interface IInventoryReservationClient
{
    Task<Reservation> ReserveAsync(ReserveRequest request, CancellationToken ct);
    Task<Reservation?> FindAsync(Guid orderId, CancellationToken ct);
    Task<Reservation> ReleaseAsync(Guid orderId, CancellationToken ct);
}

public sealed class OrderingFailure(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}