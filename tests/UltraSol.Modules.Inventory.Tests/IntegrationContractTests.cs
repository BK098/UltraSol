using System.Text.Json;
using UltraSol.Shared.IntegrationEvents.Inventory;
using Xunit;

namespace UltraSol.Modules.Inventory.Tests;

public sealed class IntegrationContractTests
{
    [Theory]
    [InlineData("OrderPlacedV1", """{"EventId":"00000000-0000-0000-0000-000000000001","CorrelationId":"00000000-0000-0000-0000-000000000002","ContractVersion":1,"OccurredAt":"2030-01-01T00:00:00+00:00","OrderId":"00000000-0000-0000-0000-000000000003","OrderVersion":2,"OrderNumber":"ORD-0000000001","BuyerType":"Guest","IdentityUserId":null,"CustomerId":null,"BusinessAccountId":null,"BuyerName":"Buyer","BuyerEmail":"buyer@example.com","BuyerPhone":"0900000000","GrandTotal":100000,"Currency":"VND","PaymentTerm":"COD","NetDays":null,"ReservationExpiresAt":"2030-01-01T00:15:00+00:00"}""")]
    [InlineData("OrderCancelledV1", """{"EventId":"00000000-0000-0000-0000-000000000001","CorrelationId":"00000000-0000-0000-0000-000000000002","ContractVersion":1,"OccurredAt":"2030-01-01T00:00:00+00:00","OrderId":"00000000-0000-0000-0000-000000000003","OrderVersion":3,"Reason":"InventoryReservationExpired"}""")]
    [InlineData("OrderCompletedV1", """{"EventId":"00000000-0000-0000-0000-000000000001","CorrelationId":"00000000-0000-0000-0000-000000000002","ContractVersion":1,"OccurredAt":"2030-01-01T00:00:00+00:00","OrderId":"00000000-0000-0000-0000-000000000003","OrderVersion":4,"BuyerType":"Guest","IdentityUserId":null,"CustomerId":null,"BusinessAccountId":null,"BuyerName":"Buyer","BuyerEmail":"buyer@example.com","BuyerPhone":"0900000000"}""")]
    public void OrderingLifecycleContractsRoundTripSerializedFixtures(string contractName, string fixture)
    {
        var contract = typeof(InventoryReservationExpiredV1).Assembly.GetType("UltraSol.Shared.IntegrationEvents.Ordering." + contractName);

        Assert.NotNull(contract);
        var message = JsonSerializer.Deserialize(fixture, contract);
        Assert.NotNull(message);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(message, contract));
        Assert.Equal(1, json.RootElement.GetProperty("ContractVersion").GetInt32());
        Assert.True(json.RootElement.GetProperty("OrderVersion").GetInt64() > 0);
        Assert.NotEqual(Guid.Empty, json.RootElement.GetProperty("OrderId").GetGuid());
    }
}