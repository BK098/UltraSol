using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UltraSol.Modules.Payment.Application;
using UltraSol.Modules.Payment.Application.Contracts;

namespace UltraSol.Modules.Payment.Infrastructure.Integrations;

public sealed class VnpayGateway(HttpClient client, VnpayOptions options, TimeProvider clock) : IVnpayGateway
{
    private static readonly string[] ResponseFields = ["vnp_ResponseId", "vnp_Command", "vnp_ResponseCode", "vnp_Message", "vnp_TmnCode",
        "vnp_TxnRef", "vnp_Amount", "vnp_BankCode", "vnp_PayDate", "vnp_TransactionNo", "vnp_TransactionType", "vnp_TransactionStatus", "vnp_OrderInfo"];

    public string CreateUrl(VnpayPayment payment)
    {
        Configure();
        Require(payment.Currency == "VND" && payment.Amount >= 5000m, "VNPAY requires VND and a minimum amount of 5000.");
        Require(payment.ExpiresAt > payment.CreatedAt && IPAddress.TryParse(payment.IpAddress, out _), "Invalid payment expiry or IP address.");
        Require(payment.Reference.Length is > 0 and <= 100 && payment.Reference.All(char.IsAsciiLetterOrDigit), "Invalid transaction reference.");
        var values = new Dictionary<string, string>
        {
            ["vnp_Version"] = "2.1.0", ["vnp_Command"] = "pay", ["vnp_TmnCode"] = options.TmnCode,
            ["vnp_Amount"] = Amount(payment.Amount), ["vnp_CurrCode"] = "VND", ["vnp_TxnRef"] = payment.Reference,
            ["vnp_OrderInfo"] = "Thanh toan " + payment.Reference, ["vnp_OrderType"] = "other", ["vnp_Locale"] = "vn",
            ["vnp_ReturnUrl"] = options.ReturnUrl, ["vnp_IpAddr"] = payment.IpAddress,
            ["vnp_CreateDate"] = Date(payment.CreatedAt), ["vnp_ExpireDate"] = Date(payment.ExpiresAt)
        };
        var query = Canonical(values);
        return options.GatewayUrl + "?" + query + "&vnp_SecureHash=" + Sign(query);
    }

    public VnpayResult VerifyCallback(IReadOnlyDictionary<string, string> values)
    {
        var verified = !string.IsNullOrEmpty(options.HashSecret) && Get(values, "vnp_TmnCode") == options.TmnCode
            && Verify(Canonical(values), Get(values, "vnp_SecureHash"));
        return Result(values, verified);
    }

    public Task<VnpayResult?> QueryAsync(VnpayPayment payment, CancellationToken ct)
    {
        Configure();
        var values = new Dictionary<string, string>
        {
            ["vnp_RequestId"] = Guid.CreateVersion7().ToString("N"), ["vnp_Version"] = "2.1.0", ["vnp_Command"] = "querydr",
            ["vnp_TmnCode"] = options.TmnCode, ["vnp_TxnRef"] = payment.Reference, ["vnp_TransactionDate"] = Date(payment.CreatedAt),
            ["vnp_CreateDate"] = Date(clock.GetUtcNow()), ["vnp_IpAddr"] = options.ServerIpAddress, ["vnp_OrderInfo"] = "Truy van " + payment.Reference
        };
        values["vnp_SecureHash"] = Sign(string.Join('|', values.Values));
        return SendAsync(options.QueryUrl, values, "querydr", ct);
    }

    public Task<VnpayResult?> RefundAsync(VnpayRefund refund, CancellationToken ct)
    {
        Configure();
        Require(refund.RequestId.Length is > 0 and <= 32 && refund.RequestId.All(char.IsAsciiLetterOrDigit), "Invalid refund request reference.");
        var values = new Dictionary<string, string>
        {
            ["vnp_RequestId"] = refund.RequestId, ["vnp_Version"] = "2.1.0", ["vnp_Command"] = "refund", ["vnp_TmnCode"] = options.TmnCode,
            ["vnp_TransactionType"] = "02", ["vnp_TxnRef"] = refund.Payment.Reference, ["vnp_Amount"] = Amount(refund.Payment.Amount),
            ["vnp_TransactionNo"] = refund.TransactionNo, ["vnp_TransactionDate"] = Date(refund.Payment.CreatedAt),
            ["vnp_CreateBy"] = refund.ApprovedBy.ToString("N"), ["vnp_CreateDate"] = Date(refund.CreatedAt),
            ["vnp_IpAddr"] = options.ServerIpAddress, ["vnp_OrderInfo"] = "Hoan tien " + refund.Payment.Reference
        };
        values["vnp_SecureHash"] = Sign(string.Join('|', values.Values));
        return SendAsync(options.RefundUrl, values, "refund", ct);
    }

    private async Task<VnpayResult?> SendAsync(string url, Dictionary<string, string> values, string command, CancellationToken ct)
    {
        try
        {
            using var response = await client.PostAsJsonAsync(url, values, ct);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }
            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            if (json.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var field in json.RootElement.EnumerateObject())
            {
                if (!result.TryAdd(field.Name, field.Value.ValueKind == JsonValueKind.String ? field.Value.GetString()! : field.Value.GetRawText()))
                {
                    return null;
                }
            }
            var names = command == "querydr" ? ResponseFields.Concat(["vnp_PromotionCode", "vnp_PromotionAmount"]) : ResponseFields;
            var verified = Get(result, "vnp_TmnCode") == options.TmnCode && Get(result, "vnp_Command") == command
                && Verify(string.Join('|', names.Select(name => Get(result, name))), Get(result, "vnp_SecureHash"));
            return Result(result, verified);
        }
        catch (Exception error) when (error is HttpRequestException or JsonException or IOException)
        {
            return null;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return null;
        }
    }

    private static VnpayResult Result(IReadOnlyDictionary<string, string> values, bool verified)
    {
        var rawAmount = Get(values, "vnp_Amount");
        decimal? amount = rawAmount.Length is > 0 and <= 12 && rawAmount.All(char.IsAsciiDigit)
            && decimal.TryParse(rawAmount, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value / 100m : null;
        DateTimeOffset? paidAt = DateTime.TryParseExact(Get(values, "vnp_PayDate"), "yyyyMMddHHmmss", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var paid) ? new DateTimeOffset(paid, TimeSpan.FromHours(7)).ToUniversalTime() : null;
        return new(verified, Get(values, "vnp_TxnRef"), amount, values.GetValueOrDefault("vnp_CurrCode"), Get(values, "vnp_ResponseCode"),
            Get(values, "vnp_TransactionStatus"), Get(values, "vnp_TransactionNo"), paidAt, Get(values, "vnp_TransactionType"));
    }

    private void Configure()
    {
        if (string.IsNullOrWhiteSpace(options.TmnCode) || string.IsNullOrWhiteSpace(options.HashSecret)
            || !SecureUrl(options.GatewayUrl) || !SecureUrl(options.QueryUrl) || !SecureUrl(options.RefundUrl) || !SecureUrl(options.ReturnUrl)
            || !IPAddress.TryParse(options.ServerIpAddress, out _))
        {
            throw new PaymentFailure(503, "VnpayNotConfigured", "VNPAY settings are incomplete.");
        }
    }

    private static bool SecureUrl(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
        && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Fragment) && string.IsNullOrEmpty(uri.Query);
    private static string Amount(decimal value)
    {
        var scaled = checked(value * 100m);
        Require(scaled > 0m && scaled <= 999999999999m && decimal.Truncate(scaled) == scaled, "VNPAY amount must be exact and fit 12 digits.");
        return scaled.ToString("0", CultureInfo.InvariantCulture);
    }
    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new PaymentFailure(400, "InvalidVnpayRequest", message);
        }
    }
    private static string Date(DateTimeOffset value) => value.ToOffset(TimeSpan.FromHours(7)).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
    private static string Get(IReadOnlyDictionary<string, string> values, string name) => values.GetValueOrDefault(name) ?? "";
    private static string Canonical(IReadOnlyDictionary<string, string> values) => string.Join('&', values
        .Where(pair => pair.Key.StartsWith("vnp_", StringComparison.Ordinal) && pair.Key is not ("vnp_SecureHash" or "vnp_SecureHashType") && !string.IsNullOrEmpty(pair.Value))
        .OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => WebUtility.UrlEncode(pair.Key) + "=" + WebUtility.UrlEncode(pair.Value)));
    private string Sign(string value) => Convert.ToHexStringLower(HMACSHA512.HashData(Encoding.UTF8.GetBytes(options.HashSecret), Encoding.UTF8.GetBytes(value)));
    private bool Verify(string value, string signature)
    {
        if (signature.Length != 128)
        {
            return false;
        }
        try
        {
            return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(Sign(value)), Convert.FromHexString(signature));
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
