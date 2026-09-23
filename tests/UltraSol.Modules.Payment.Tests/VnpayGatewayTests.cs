using System.Net;
using System.Security.Cryptography;
using System.Text;
using UltraSol.Modules.Payment.Application;
using UltraSol.Modules.Payment.Application.Contracts;
using UltraSol.Modules.Payment.Infrastructure.Integrations;
using Xunit;

namespace UltraSol.Modules.Payment.Tests;

public sealed class VnpayGatewayTests
{
    private static readonly VnpayOptions Options = new() { TmnCode = "TESTCODE", HashSecret = "test-only-key", ReturnUrl = "https://merchant.example/api/payments/vnpay/return" };
    private static readonly DateTimeOffset Now = new(2026, 9, 23, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Url_is_signed_exactly_and_uses_reserved_expiry()
    {
        var gateway = Gateway();
        var url = gateway.CreateUrl(new("ref1", 5000.25m, "VND", Now, Now.AddMinutes(3), "127.0.0.1"));
        Assert.Contains("vnp_Amount=500025", url);
        Assert.Contains("vnp_CreateDate=20260923170000", url);
        Assert.Contains("vnp_ExpireDate=20260923170300", url);
        var query = url[(url.IndexOf('?') + 1)..];
        var parts = query.Split("&vnp_SecureHash=");
        Assert.Equal(Sign(parts[0]), parts[1]);
        Assert.Throws<PaymentFailure>(() => gateway.CreateUrl(new("ref1", 4999m, "VND", Now, Now.AddMinutes(3), "127.0.0.1")));
        Assert.Throws<PaymentFailure>(() => gateway.CreateUrl(new("ref1", 5000.001m, "VND", Now, Now.AddMinutes(3), "127.0.0.1")));
        Assert.Throws<PaymentFailure>(() => gateway.CreateUrl(new("ref1", 5000m, "USD", Now, Now.AddMinutes(3), "127.0.0.1")));
    }

    [Fact]
    public void Callback_checks_hash_merchant_and_malformed_amount_without_throwing()
    {
        var fields = Callback();
        var result = Gateway().VerifyCallback(fields);
        Assert.True(result.Verified);
        Assert.Equal(5000m, result.Amount);
        fields["vnp_Amount"] = "500001";
        Assert.False(Gateway().VerifyCallback(fields).Verified);
        fields["vnp_TmnCode"] = "OTHER";
        SignCallback(fields);
        Assert.False(Gateway().VerifyCallback(fields).Verified);
        fields["vnp_Amount"] = "not-money";
        SignCallback(fields);
        Assert.Null(Gateway().VerifyCallback(fields).Amount);
    }

    [Fact]
    public async Task Query_verifies_pipe_signature_and_does_not_trust_unsigned_success()
    {
        var handler = new StubHandler();
        var gateway = new VnpayGateway(new HttpClient(handler), Options, new FixedTime());
        var payment = new VnpayPayment("ref1", 5000, "VND", Now, Now.AddMinutes(3), "127.0.0.1");
        handler.Response = "{\"vnp_ResponseCode\":\"00\",\"vnp_TransactionStatus\":\"00\",\"vnp_TxnRef\":\"ref1\",\"vnp_Amount\":\"500000\"}";
        Assert.False((await gateway.QueryAsync(payment, default))!.Verified);
        Assert.Contains("\"vnp_Command\":\"querydr\"", handler.Request);
        Assert.Contains("\"vnp_TransactionDate\":\"20260923170000\"", handler.Request);
    }

    internal static Dictionary<string, string> Callback()
    {
        var fields = new Dictionary<string, string> { ["vnp_TmnCode"] = "TESTCODE", ["vnp_TxnRef"] = "ref1", ["vnp_Amount"] = "500000",
            ["vnp_ResponseCode"] = "00", ["vnp_TransactionStatus"] = "00", ["vnp_TransactionNo"] = "123456", ["vnp_PayDate"] = "20260923170100" };
        SignCallback(fields);
        return fields;
    }

    internal static void SignCallback(Dictionary<string, string> values) => values["vnp_SecureHash"] = Sign(string.Join("&", values
        .Where(pair => pair.Key != "vnp_SecureHash").OrderBy(pair => pair.Key, StringComparer.Ordinal)
        .Select(pair => WebUtility.UrlEncode(pair.Key) + "=" + WebUtility.UrlEncode(pair.Value))));
    private static string Sign(string value) => Convert.ToHexStringLower(HMACSHA512.HashData(Encoding.UTF8.GetBytes(Options.HashSecret), Encoding.UTF8.GetBytes(value)));
    private static VnpayGateway Gateway() => new(new HttpClient(new StubHandler()), Options, new FixedTime());
    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class StubHandler : HttpMessageHandler
    {
        public string Response { get; set; } = "{}";
        public string Request { get; private set; } = "";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Request = await request.Content!.ReadAsStringAsync(ct);
            return new(HttpStatusCode.OK) { Content = new StringContent(Response, Encoding.UTF8, "application/json") };
        }
    }
}
