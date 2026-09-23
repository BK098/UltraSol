namespace UltraSol.Modules.Payment.Application.Contracts;

public sealed record VnpayPayment(string Reference, decimal Amount, string Currency, DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt, string IpAddress);
public sealed record VnpayRefund(string RequestId, VnpayPayment Payment, string TransactionNo, Guid ApprovedBy, DateTimeOffset CreatedAt);
public sealed record VnpayResult(bool Verified, string Reference, decimal? Amount, string? Currency, string ResponseCode,
    string Status, string TransactionNo, DateTimeOffset? PaidAt, string TransactionType);

public interface IVnpayGateway
{
    string CreateUrl(VnpayPayment payment);
    VnpayResult VerifyCallback(IReadOnlyDictionary<string, string> values);
    Task<VnpayResult?> QueryAsync(VnpayPayment payment, CancellationToken ct);
    Task<VnpayResult?> RefundAsync(VnpayRefund refund, CancellationToken ct);
}
