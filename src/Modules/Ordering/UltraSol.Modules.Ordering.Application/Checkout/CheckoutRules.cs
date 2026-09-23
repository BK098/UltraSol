using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UltraSol.Modules.Ordering.Domain.ValueObjects;
using UltraSol.Shared.Domain.Common.Exceptions;

namespace UltraSol.Modules.Ordering.Application.Checkout;

public static class CheckoutRules
{
    public static string Fingerprint<T>(T request) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))));
    public static DateTimeOffset Normalize(DateTimeOffset value) => new(value.UtcTicks - value.UtcTicks % 10, TimeSpan.Zero);
    public static RequestedLine[] NormalizeLines(RequestedLine[] lines)
    {
        OrderingRule.Require(lines is { Length: > 0 and <= 100 }, "Supply 1 to 100 lines.");
        foreach (var line in lines)
        {
            OrderingRule.Require(line is not null && line.ProductItemId != Guid.Empty && line.Quantity > 0 && line.NegotiatedPriceId != Guid.Empty, "Invalid line.");
        }
        OrderingRule.Require(lines.GroupBy(line => line.ProductItemId).All(group => group.Select(line => line.NegotiatedPriceId).Distinct().Count() == 1), "Conflicting negotiated contexts.");
        return lines.GroupBy(line => line.ProductItemId).Select(group => new RequestedLine(group.Key,
            group.Aggregate(0, (sum, line) => checked(sum + line.Quantity)), group.First().NegotiatedPriceId)).OrderBy(line => line.ProductItemId).ToArray();
    }

    public static StockLine[] Expand(RequestedLine[] lines, CatalogItem[] items)
    {
        if (items is null || items.Any(item => item is null))
        {
            throw new OrderingFailure(503, "InvalidCatalogResponse", "Catalog returned an invalid item.");
        }
        var result = new Dictionary<Guid, int>();
        foreach (var line in lines)
        {
            var matches = items.Where(item => item.ProductItemId == line.ProductItemId).ToArray();
            if (matches.Length != 1 || !matches[0].IsSellable)
            {
                throw new OrderingFailure(409, "ProductNotSellable", "An item is missing or cannot be sold.");
            }
            var item = matches[0];
            var physical = item.IsBundle ? item.Components : [new StockLine(item.ProductItemId, 1)];
            if (physical is null || physical.Length == 0 || physical.Any(component => component is null || component.ProductItemId == Guid.Empty || component.Quantity <= 0))
            {
                throw new OrderingFailure(503, "InvalidCatalogResponse", "Catalog returned an invalid bundle.");
            }
            foreach (var component in physical)
            {
                result[component.ProductItemId] = checked(result.GetValueOrDefault(component.ProductItemId) + component.Quantity * line.Quantity);
            }
        }
        return result.OrderBy(pair => pair.Key).Select(pair => new StockLine(pair.Key, pair.Value)).ToArray();
    }

    public static void ValidateQuote(QuoteRequest input, PriceQuote quote)
    {
        if (quote.QuoteId == Guid.Empty || quote.Currency != input.Currency || quote.Lines is null || quote.Lines.Length != input.Lines.Length || quote.Lines.Any(line => line is null)
            || quote.Lines.Select(line => line.ProductItemId).Distinct().Count() != input.Lines.Length)
        {
            throw new OrderingFailure(503, "InvalidPricingResponse", "Pricing returned an incomplete quote.");
        }
        try
        {
            decimal subtotal = 0;
            decimal discount = 0;
            foreach (var requested in input.Lines)
            {
                var line = quote.Lines.SingleOrDefault(line => line.ProductItemId == requested.ProductItemId);
                if (line is null || line.Quantity != requested.Quantity || line.LineTotal != checked(line.FinalUnitPrice * line.Quantity)
                    || line.NegotiatedPriceId != requested.NegotiatedPriceId || line.ContractId != input.ContractId)
                {
                    throw new OrderingFailure(503, "InvalidPricingResponse", "Pricing returned a different context.");
                }
                line.Snapshot(quote.Currency).Validate(quote.Currency, line.Quantity);
                subtotal = checked(subtotal + line.ListUnitPrice * line.Quantity);
                discount = checked(discount + line.DiscountAmount);
            }
            if (quote.Subtotal != subtotal || quote.DiscountTotal != discount || quote.GrandTotal != subtotal - discount
                || quote.TaxTotal != 0 || quote.ShippingAmount != 0)
            {
                throw new OrderingFailure(503, "InvalidPricingResponse", "Pricing totals are inconsistent.");
            }
        }
        catch (Exception error) when (error is DomainException or OverflowException)
        {
            throw new OrderingFailure(503, "InvalidPricingResponse", "Pricing returned invalid prices.");
        }
    }

    public static void ValidateReservation(CheckoutAttempt attempt, Reservation reservation)
    {
        if (reservation.Lines is null || reservation.Lines.Any(line => line is null))
        {
            throw new OrderingFailure(503, "InvalidInventoryResponse", "Inventory returned invalid reservation lines.");
        }
        var actual = reservation.Lines?.Select(line => new StockLine(line.ProductItemId, line.Quantity)).OrderBy(line => line.ProductItemId).ToArray();
        if (reservation.Id == Guid.Empty || attempt.ReservationId is { } id && id != reservation.Id
            || reservation.OrderId != attempt.OrderId || reservation.ExpiresAt != attempt.ExpiresAt
            || reservation.Lines?.Any(line => line.WarehouseId == Guid.Empty) == true
            || reservation.Lines?.Select(line => line.WarehouseId).Distinct().Count() != 1
            || actual is null || !actual.SequenceEqual(attempt.StockLines!))
        {
            throw new OrderingFailure(503, "InvalidInventoryResponse", "Inventory returned a different reservation.");
        }
    }
}