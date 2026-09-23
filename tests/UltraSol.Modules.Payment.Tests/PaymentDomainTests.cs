using UltraSol.Modules.Payment.Domain.Payments;
using UltraSol.Shared.Domain.Common.Exceptions;
using Xunit;
using PaymentAggregate = UltraSol.Modules.Payment.Domain.Payments.Payment;

namespace UltraSol.Modules.Payment.Tests;

public sealed class PaymentDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 23, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Zero_amount_requires_no_payment_and_money_must_have_two_decimals()
    {
        var payment = Snapshot(0);
        Assert.True(payment.NoPaymentRequired);
        Assert.Throws<DomainException>(() => Snapshot(1.001m));
        Assert.Throws<DomainException>(() => Snapshot(-1m));
    }

    [Fact]
    public void Exact_receipt_is_applied_and_duplicate_key_with_other_data_conflicts()
    {
        var payment = Snapshot(100);
        var receipt = payment.CreateTransaction("BankTransfer", 100, "VND", "key", "fingerprint", "bank", "transfer-1", null, Guid.NewGuid(), "confirmed", Now);
        payment.RecordTransactionSuccess(receipt.Id, null, null, Now);
        Assert.True(payment.IsPaid);
        Assert.True(receipt.Applied);
        Assert.Same(receipt, payment.CreateTransaction("BankTransfer", 100, "VND", "key", "fingerprint", "bank", "transfer-1", null, null, null, Now));
        Assert.Throws<DomainException>(() => payment.CreateTransaction("BankTransfer", 99, "VND", "key", "different", "bank", "transfer-2", null, null, null, Now));
        Assert.Throws<DomainException>(() => payment.CreateTransaction("BankTransfer", 100, "VND", "other", "other", "bank", "transfer-1", null, null, null, Now));
    }

    [Fact]
    public void Two_actual_successes_keep_both_and_surplus_requests_refund()
    {
        var payment = Snapshot(100);
        var first = payment.CreateTransaction("Vnpay", 100, "VND", "one", "fp1", "Vnpay", null, "provider-1", null, null, Now);
        payment.RecordTransactionSuccess(first.Id, "provider-1", "txn-1", Now);
        var second = payment.CreateTransaction("BankTransfer", 100, "VND", "two", "fp2", "bank", "transfer-2", null, null, null, Now);
        payment.RecordTransactionSuccess(second.Id, null, null, Now);
        Assert.True(first.Applied);
        Assert.False(second.Applied);
        Assert.Equal(2, payment.Transactions.Count(transaction => transaction.Status == PaymentTransactionStatus.Succeeded));
        Assert.Equal(second.Id, Assert.Single(payment.Refunds).TransactionId);
    }

    [Fact]
    public void Failed_attempt_can_later_report_real_money_and_duplicate_callback_keeps_first_receipt_time()
    {
        var payment = Snapshot(100);
        var receipt = payment.CreateTransaction("Vnpay", 100, "VND", "one", "fp1", "Vnpay", null, "provider-1", null, null, Now);
        payment.RecordTransactionFailure(receipt.Id, Now.AddMinutes(1));
        payment.RecordTransactionSuccess(receipt.Id, "provider-1", "txn-1", Now.AddMinutes(2));
        payment.RecordTransactionSuccess(receipt.Id, "provider-1", "txn-1", Now.AddMinutes(3));
        Assert.True(payment.IsPaid);
        Assert.Equal(Now.AddMinutes(2), receipt.ReceivedAt);
    }

    [Fact]
    public void Unexpected_money_on_zero_amount_order_requests_full_refund()
    {
        var payment = Snapshot(0);
        Assert.Throws<DomainException>(() => payment.CreateTransaction("Vnpay", 10, "VND", "one", "fp1", "Vnpay", null,
            "provider-1", null, null, Now));
    }

    [Fact]
    public void Initiated_receipts_require_snapshot_exact_amount_and_allowed_method()
    {
        var pending = PaymentAggregate.Start(Guid.NewGuid(), Now);
        Assert.Throws<DomainException>(() => pending.CreateTransaction("Vnpay", 100, "VND", "key", "fp", "Vnpay", null,
            "provider-1", null, null, Now));
        var payment = Snapshot(100);
        Assert.Throws<DomainException>(() => payment.CreateTransaction("Vnpay", 99, "VND", "key", "fp", "Vnpay", null,
            "provider-1", null, null, Now));
        Assert.Throws<DomainException>(() => payment.CreateTransaction("COD", 100, "VND", "key", "fp", "manual", "ref",
            null, null, null, Now));
        payment.ApplyOrderSignal(2, "Cancelled", null, Now);
        Assert.Throws<DomainException>(() => payment.CreateTransaction("Vnpay", 100, "VND", "key", "fp", "Vnpay", null,
            "provider-1", null, null, Now));
    }

    [Fact]
    public void Contradictory_success_is_flagged_without_erasing_receipt()
    {
        var payment = Snapshot(100);
        var receipt = payment.CreateTransaction("Vnpay", 100, "VND", "key", "fp", "Vnpay", null, "provider-1", null, null, Now);
        payment.RecordTransactionSuccess(receipt.Id, "provider-1", "txn-1", Now);
        payment.RecordTransactionSuccess(receipt.Id, "provider-1", "txn-2", Now.AddMinutes(1));
        Assert.Equal(PaymentTransactionStatus.Succeeded, receipt.Status);
        Assert.Equal("txn-1", receipt.ProviderTransactionNo);
        Assert.True(receipt.NeedsReconciliation);
    }

    [Fact]
    public void Cancelled_and_late_success_request_full_refund()
    {
        var payment = Snapshot(100);
        var receipt = payment.CreateTransaction("Vnpay", 100, "VND", "one", "fp1", "Vnpay", null, "provider-1", null, null, Now);
        payment.ApplyOrderSignal(2, "Cancelled", null, Now);
        payment.RecordTransactionSuccess(receipt.Id, "provider-1", "txn-1", Now);
        Assert.False(payment.IsPaid);
        Assert.Equal(100, Assert.Single(payment.Refunds).Amount);
    }

    [Fact]
    public void Out_of_order_terminal_signal_survives_snapshot_and_net_due_date_does_not_shift()
    {
        var payment = PaymentAggregate.Start(Guid.NewGuid(), Now);
        payment.ApplyOrderSignal(3, "Completed", Now, Now);
        payment.ApplySnapshot(1, "Placed", "ORD-1", 100, "VND", "Net", 30, Now.AddMinutes(10), null, Now);
        Assert.Equal("Completed", payment.OrderStatus);
        Assert.Equal(Now.AddDays(30), payment.DueAt);
        payment.ApplyOrderSignal(3, "Completed", Now.AddDays(1), Now.AddDays(1));
        Assert.Equal(Now.AddDays(30), payment.DueAt);
    }

    [Fact]
    public void Refund_needs_approval_and_settlement_evidence()
    {
        var payment = Snapshot(100);
        var receipt = payment.CreateTransaction("Vnpay", 100, "VND", "one", "fp1", "Vnpay", null, "provider-1", null, null, Now);
        payment.RecordTransactionSuccess(receipt.Id, "provider-1", "txn-1", Now);
        payment.ApplyOrderSignal(2, "Rejected", null, Now);
        var refund = Assert.Single(payment.Refunds);
        payment.ApproveRefund(refund.Id, Guid.NewGuid(), Now);
        Assert.Throws<DomainException>(() => payment.ApproveRefund(refund.Id, Guid.NewGuid(), Now));
        payment.BeginRefundProcessing(refund.Id, "request-1", Now);
        payment.RecordRefundOutcome(refund.Id, "Unknown", Now);
        payment.ConfirmRefundSettled(refund.Id, "bank-evidence", Guid.NewGuid(), Now);
        Assert.Equal(PaymentRefundState.Refunded, refund.State);
    }

    private static PaymentAggregate Snapshot(decimal amount)
    {
        var payment = PaymentAggregate.Start(Guid.NewGuid(), Now);
        payment.ApplySnapshot(1, "Placed", "ORD-1", amount, "VND", "Prepaid", null, Now.AddMinutes(10), null, Now);
        return payment;
    }
}
