using MediatR;
using Microsoft.AspNetCore.Mvc;
using UltraSol.Modules.Payment.Application;
using UltraSol.Modules.Payment.Application.Features.Payments;
using UltraSol.Modules.Payment.Application.Features.Receipts;
using UltraSol.Modules.Payment.Application.Features.Refunds;
using UltraSol.Modules.Payment.Application.Features.Reconciliation;
using UltraSol.Shared.Infrastructure.Api;

namespace UltraSol.Modules.Payment.Api.Controllers;

[Route("api/payments")]
internal sealed class PaymentsController(ISender sender) : BaseController(sender)
{
    public sealed record Approval(string ConcurrencyStamp);
    public sealed record RefundEvidence(string ConcurrencyStamp, string EvidenceReference);
    public sealed record ReceiptEvidence(string ConcurrencyStamp, bool Received, string EvidenceReference, DateTimeOffset ReceivedAt, string? ProviderTransactionNo);

    [HttpGet]
    public Task<IActionResult> List([FromQuery] int skip = 0, [FromQuery] int take = 20, [FromQuery] bool overdue = false, CancellationToken ct = default) =>
        SendAsync(new GetPaymentsQuery(skip, take, overdue), ct);

    [HttpGet("orders/{id:guid}")]
    public Task<IActionResult> Detail(Guid id, CancellationToken ct) => SendAsync(new GetPaymentQuery(id), ct);

    [HttpPost("orders/{id:guid}/receipts")]
    public Task<IActionResult> Receipt(Guid id, ManualReceipt body, CancellationToken ct) =>
        SendAsync(new RecordReceiptCommand(id, Request.Headers["Idempotency-Key"].ToString(), body), ct);

    [HttpGet("refunds")]
    public Task<IActionResult> Refunds([FromQuery] int skip = 0, [FromQuery] int take = 20, [FromQuery] string? state = null, CancellationToken ct = default) =>
        SendAsync(new GetRefundsQuery(skip, take, state), ct);

    [HttpPost("orders/{id:guid}/refunds/{refundId:guid}/approve")]
    public Task<IActionResult> Approve(Guid id, Guid refundId, Approval body, CancellationToken ct) =>
        SendAsync(new ApproveRefundCommand(id, refundId, body.ConcurrencyStamp), ct);

    [HttpPost("orders/{id:guid}/refunds/{refundId:guid}/confirm")]
    public Task<IActionResult> Confirm(Guid id, Guid refundId, RefundEvidence body, CancellationToken ct) =>
        SendAsync(new ConfirmRefundCommand(id, refundId, body.ConcurrencyStamp, body.EvidenceReference), ct);

    [HttpPost("orders/{id:guid}/transactions/{transactionId:guid}/query")]
    public Task<IActionResult> Query(Guid id, Guid transactionId, CancellationToken ct) => SendAsync(new QueryTransactionCommand(id, transactionId), ct);

    [HttpPost("orders/{id:guid}/transactions/{transactionId:guid}/reconcile")]
    public Task<IActionResult> Reconcile(Guid id, Guid transactionId, ReceiptEvidence body, CancellationToken ct) =>
        SendAsync(new ReconcileTransactionCommand(id, transactionId, body.ConcurrencyStamp, body.Received, body.EvidenceReference, body.ReceivedAt, body.ProviderTransactionNo), ct);
}
