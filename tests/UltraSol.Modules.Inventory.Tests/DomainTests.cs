using UltraSol.Modules.Inventory.Domain;
using UltraSol.Modules.Inventory.Domain.Adjustments;
using UltraSol.Modules.Inventory.Domain.Reservations;
using UltraSol.Modules.Inventory.Domain.Stocks;
using UltraSol.Shared.Domain.Common.Exceptions;
using Xunit;

namespace UltraSol.Modules.Inventory.Tests;

public sealed class DomainTests
{
    private static readonly Guid WarehouseId = InventoryDefaults.WarehouseId;
    private static readonly Guid ProductItemId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly DateTimeOffset Now = new(2026, 9, 15, 1, 0, 0, TimeSpan.FromHours(7));

    [Fact]
    public void StockOperationsPreserveQuantities()
    {
        var stock = new InventoryStock(WarehouseId, ProductItemId);

        stock.Receive(10);
        stock.Reserve(4);
        stock.Release(1);
        stock.IssueReserved(2);
        stock.Adjust(-1);

        Assert.Equal(7, stock.OnHand);
        Assert.Equal(1, stock.Reserved);
        Assert.Equal(6, stock.Available);
    }

    [Fact]
    public void StockRejectsInvalidIdsQuantitiesAndInvariants()
    {
        Assert.Throws<DomainException>(() => new InventoryStock(Guid.Empty, ProductItemId));
        Assert.Throws<DomainException>(() => new InventoryStock(WarehouseId, Guid.Empty));

        var stock = new InventoryStock(WarehouseId, ProductItemId);
        AssertInvalidQuantity(() => stock.Receive(0));
        AssertInvalidQuantity(() => stock.Reserve(1));
        stock.Receive(2);
        AssertInvalidQuantity(() => stock.Reserve(3));
        stock.Reserve(2);
        AssertInvalidQuantity(() => stock.Release(3));
        AssertInvalidQuantity(() => stock.IssueReserved(3));
        AssertInvalidQuantity(() => stock.Adjust(0));
        AssertInvalidQuantity(() => stock.Adjust(-1));
    }

    [Fact]
    public void StockTranslatesCheckedOverflowToInvalidQuantity()
    {
        var receiveOverflow = new InventoryStock(WarehouseId, ProductItemId);
        receiveOverflow.Receive(int.MaxValue);
        AssertInvalidQuantity(() => receiveOverflow.Receive(1));

        var adjustOverflow = new InventoryStock(WarehouseId, ProductItemId);
        adjustOverflow.Receive(int.MaxValue);
        AssertInvalidQuantity(() => adjustOverflow.Adjust(1));
        AssertInvalidQuantity(() => adjustOverflow.Adjust(int.MinValue));
    }

    [Fact]
    public void StockCannotUseGenericDeletion()
    {
        var stock = new InventoryStock(WarehouseId, ProductItemId);

        Assert.Throws<DomainException>(() => stock.MarkDeleted("actor", Now));
        Assert.Throws<DomainException>(stock.Restore);
    }

    [Fact]
    public void ReservationNormalizesAndMatchesBusinessContentInAnyOrder()
    {
        var otherProduct = Guid.Parse("00000000-0000-0000-0000-000000000003");
        var expiresAt = Now.AddHours(1);
        var reservation = new StockReservation(
            Guid.NewGuid(),
            expiresAt,
            [new ReservationLine(WarehouseId, ProductItemId, 2), new ReservationLine(WarehouseId, otherProduct, 3)],
            Now);

        var matches = reservation.Matches(
            expiresAt.ToOffset(TimeSpan.FromHours(-4)),
            [new ReservationLine(WarehouseId, otherProduct, 3), new ReservationLine(WarehouseId, ProductItemId, 2)]);

        Assert.True(matches);
        Assert.Equal(expiresAt.ToUniversalTime(), reservation.ExpiresAt);
        Assert.False(reservation.Matches(expiresAt, [new ReservationLine(WarehouseId, ProductItemId, 2)]));
    }

    [Fact]
    public void ReservationRejectsInvalidOrDuplicateLines()
    {
        var orderId = Guid.NewGuid();
        Assert.Throws<DomainException>(() => new StockReservation(Guid.Empty, Now.AddHours(1), [Line()], Now));
        Assert.Throws<DomainException>(() => new StockReservation(orderId, Now, [Line()], Now));
        Assert.Throws<DomainException>(() => new StockReservation(orderId, Now.AddHours(1), [], Now));
        Assert.Throws<DomainException>(() => new StockReservation(orderId, Now.AddHours(1), [Line(), Line()], Now));
        Assert.Throws<DomainException>(() => new ReservationLine(Guid.Empty, ProductItemId, 1));
        Assert.Throws<DomainException>(() => new ReservationLine(WarehouseId, Guid.Empty, 1));
        AssertInvalidQuantity(() => new ReservationLine(WarehouseId, ProductItemId, 0));
    }

    [Fact]
    public void ReservationReleaseIsReplaySafeAndTerminal()
    {
        var reservation = Reservation();

        Assert.True(reservation.Release(Now));
        Assert.False(reservation.Release(Now.AddMinutes(1)));
        Assert.Equal(ReservationStatus.Released, reservation.Status);
        Assert.Equal(Now.ToUniversalTime(), reservation.ReleasedAt);
        Assert.Throws<DomainException>(() => reservation.Consume(Now));
        Assert.False(reservation.Expire(Now.AddHours(2)));
    }

    [Fact]
    public void ReservationConsumeIsReplaySafeAndRejectsDueReservation()
    {
        var consumed = Reservation();
        Assert.True(consumed.Consume(Now));
        Assert.False(consumed.Consume(Now.AddMinutes(1)));
        Assert.Equal(ReservationStatus.Consumed, consumed.Status);
        Assert.Equal(Now.ToUniversalTime(), consumed.ConsumedAt);
        Assert.Throws<DomainException>(() => consumed.Release(Now.AddHours(2)));

        var due = Reservation(Now);
        Assert.Equal(ReservationStatus.Expired, due.EffectiveStatus(Now));
        Assert.Throws<DomainException>(() => due.Consume(Now));
    }

    [Fact]
    public void ReservationExpiryUsesInclusiveBoundaryAndIsReplaySafe()
    {
        var expiresAt = Now.AddHours(1);
        var reservation = Reservation(expiresAt);

        Assert.False(reservation.Expire(expiresAt.AddTicks(-1)));
        Assert.True(reservation.Expire(expiresAt));
        Assert.False(reservation.Expire(expiresAt.AddMinutes(1)));
        Assert.False(reservation.Release(expiresAt.AddMinutes(1)));
        Assert.Equal(ReservationStatus.Expired, reservation.Status);
        Assert.Equal(expiresAt.ToUniversalTime(), reservation.ExpiredAt);
    }

    [Fact]
    public void ReservationCannotUseGenericDeletion()
    {
        var reservation = Reservation();

        Assert.Throws<DomainException>(() => reservation.MarkDeleted("actor", Now));
        Assert.Throws<DomainException>(reservation.Restore);
    }

    [Fact]
    public void AdjustmentNormalizesContentAndUpdatesWhileDraft()
    {
        var adjustment = new InventoryAdjustment(
            AdjustmentReason.ManualCorrection,
            "  first  ",
            [new AdjustmentLine(WarehouseId, ProductItemId, 2)]);
        var otherProduct = Guid.NewGuid();

        adjustment.Update(
            AdjustmentReason.Damaged,
            "   ",
            [new AdjustmentLine(WarehouseId, otherProduct, -1)]);

        Assert.Equal(AdjustmentReason.Damaged, adjustment.Reason);
        Assert.Null(adjustment.Note);
        Assert.Equal(AdjustmentStatus.Draft, adjustment.Status);
        Assert.Collection(adjustment.Lines, line => Assert.Equal(otherProduct, line.ProductItemId));
    }

    [Fact]
    public void AdjustmentRejectsInvalidContent()
    {
        Assert.Throws<DomainException>(() => new InventoryAdjustment((AdjustmentReason)999, null, [AdjustmentLine()]));
        Assert.Throws<DomainException>(() => new InventoryAdjustment(AdjustmentReason.ManualCorrection, null, []));
        Assert.Throws<DomainException>(() => new InventoryAdjustment(AdjustmentReason.ManualCorrection, null, [AdjustmentLine(), AdjustmentLine()]));
        Assert.Throws<DomainException>(() => new InventoryAdjustment(AdjustmentReason.ManualCorrection, new string('x', 2001), [AdjustmentLine()]));
        Assert.Throws<DomainException>(() => new AdjustmentLine(Guid.Empty, ProductItemId, 1));
        Assert.Throws<DomainException>(() => new AdjustmentLine(WarehouseId, Guid.Empty, 1));
        AssertInvalidQuantity(() => new AdjustmentLine(WarehouseId, ProductItemId, 0));
    }

    [Fact]
    public void AdjustmentPostAndCancelAreReplaySafeTerminalTransitions()
    {
        var posted = Adjustment();
        Assert.True(posted.Post(Now));
        Assert.False(posted.Post(Now.AddMinutes(1)));
        Assert.Equal(Now.ToUniversalTime(), posted.PostedAt);
        Assert.Throws<DomainException>(() => posted.Cancel(Now));
        Assert.Throws<DomainException>(() => posted.Update(AdjustmentReason.Lost, null, [AdjustmentLine()]));

        var cancelled = Adjustment();
        Assert.True(cancelled.Cancel(Now));
        Assert.False(cancelled.Cancel(Now.AddMinutes(1)));
        Assert.Equal(Now.ToUniversalTime(), cancelled.CancelledAt);
        Assert.Throws<DomainException>(() => cancelled.Post(Now));
        Assert.Throws<DomainException>(() => cancelled.Update(AdjustmentReason.Lost, null, [AdjustmentLine()]));
    }

    [Fact]
    public void AdjustmentCannotUseGenericDeletion()
    {
        var adjustment = Adjustment();

        Assert.Throws<DomainException>(() => adjustment.MarkDeleted("actor", Now));
        Assert.Throws<DomainException>(adjustment.Restore);
    }

    [Theory]
    [InlineData(MovementType.GoodsReceipt, 1)]
    [InlineData(MovementType.OrderIssue, -1)]
    [InlineData(MovementType.Adjustment, 1)]
    [InlineData(MovementType.Adjustment, -1)]
    public void MovementNormalizesImmutableLedgerEntry(MovementType type, int quantityDelta)
    {
        var id = Guid.NewGuid();
        var referenceId = Guid.NewGuid();
        var movement = new StockMovement(
            id,
            WarehouseId,
            ProductItemId,
            type,
            quantityDelta,
            "  Order  ",
            referenceId,
            "  note  ",
            "  actor  ",
            Now);

        Assert.Equal(id, movement.Id);
        Assert.Equal("Order", movement.ReferenceType);
        Assert.Equal("note", movement.Note);
        Assert.Equal("actor", movement.ActorId);
        Assert.Equal(Now.ToUniversalTime(), movement.OccurredAt);
    }

    [Fact]
    public void MovementRejectsInvalidIdsDirectionAndStrings()
    {
        Assert.Throws<DomainException>(() => Movement(id: Guid.Empty));
        Assert.Throws<DomainException>(() => Movement(warehouseId: Guid.Empty));
        Assert.Throws<DomainException>(() => Movement(productItemId: Guid.Empty));
        Assert.Throws<DomainException>(() => Movement(referenceId: Guid.Empty));
        AssertInvalidQuantity(() => Movement(quantityDelta: 0));
        AssertInvalidQuantity(() => Movement(type: MovementType.GoodsReceipt, quantityDelta: -1));
        AssertInvalidQuantity(() => Movement(type: MovementType.OrderIssue, quantityDelta: 1));
        Assert.Throws<DomainException>(() => Movement(type: (MovementType)999));
        Assert.Throws<DomainException>(() => Movement(referenceType: " "));
        Assert.Throws<DomainException>(() => Movement(referenceType: new string('x', 101)));
        Assert.Throws<DomainException>(() => Movement(note: new string('x', 2001)));
        Assert.Throws<DomainException>(() => Movement(actorId: new string('x', 257)));
    }

    private static ReservationLine Line() => new(WarehouseId, ProductItemId, 1);

    private static StockReservation Reservation(DateTimeOffset? expiresAt = null) =>
        new(Guid.NewGuid(), expiresAt ?? Now.AddHours(1), [Line()], Now.AddHours(-1));

    private static AdjustmentLine AdjustmentLine() => new(WarehouseId, ProductItemId, 1);

    private static InventoryAdjustment Adjustment() =>
        new(AdjustmentReason.ManualCorrection, null, [AdjustmentLine()]);

    private static StockMovement Movement(
        Guid? id = null,
        Guid? warehouseId = null,
        Guid? productItemId = null,
        MovementType type = MovementType.Adjustment,
        int quantityDelta = 1,
        string referenceType = "Order",
        Guid? referenceId = null,
        string? note = null,
        string? actorId = null) =>
        new(
            id ?? Guid.NewGuid(),
            warehouseId ?? WarehouseId,
            productItemId ?? ProductItemId,
            type,
            quantityDelta,
            referenceType,
            referenceId ?? Guid.NewGuid(),
            note,
            actorId,
            Now);

    private static void AssertInvalidQuantity(Action action)
    {
        var exception = Assert.Throws<DomainException>(action);
        Assert.Equal("InvalidQuantity", exception.Code);
    }
}
