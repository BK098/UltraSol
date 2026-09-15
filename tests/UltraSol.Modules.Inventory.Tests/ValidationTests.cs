using UltraSol.Modules.Inventory.Application.Contracts;
using UltraSol.Modules.Inventory.Application.Validation;
using Xunit;

namespace UltraSol.Modules.Inventory.Tests;

public sealed class ValidationTests
{
    private static readonly Guid ProductItemId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    [Fact]
    public void ReceiveStockRejectsInvalidBoundaryValues()
    {
        var validator = new ReceiveStockRequestValidator();
        var request = new ReceiveStockRequest(Guid.Empty, Guid.Empty, 0, " ", Guid.Empty, new string('x', 2001));

        var result = validator.Validate(request);

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(ReceiveStockRequest.OperationId));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(ReceiveStockRequest.ProductItemId));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(ReceiveStockRequest.Quantity));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(ReceiveStockRequest.ReferenceType));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(ReceiveStockRequest.ReferenceId));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(ReceiveStockRequest.Note));
    }

    [Fact]
    public void ReceiveStockAcceptsValidRequest()
    {
        var request = new ReceiveStockRequest(Guid.NewGuid(), ProductItemId, 1, new string('r', 100), Guid.NewGuid(), new string('n', 2000));

        Assert.True(new ReceiveStockRequestValidator().Validate(request).IsValid);
    }

    [Fact]
    public void ReserveStockRejectsNullDuplicateAndOversizedLines()
    {
        var validator = new ReserveStockRequestValidator();
        var nullLines = validator.Validate(new ReserveStockRequest(Guid.NewGuid(), DateTimeOffset.MinValue, null!));
        var nullItem = validator.Validate(new ReserveStockRequest(Guid.NewGuid(), DateTimeOffset.MinValue, [null!]));
        var duplicate = validator.Validate(new ReserveStockRequest(Guid.NewGuid(), DateTimeOffset.MinValue,
            [new StockLineRequest(ProductItemId, 1), new StockLineRequest(ProductItemId, 2)]));
        var oversized = validator.Validate(new ReserveStockRequest(Guid.NewGuid(), DateTimeOffset.MinValue,
            Enumerable.Range(0, 201).Select(_ => new StockLineRequest(Guid.NewGuid(), 1)).ToArray()));

        Assert.False(nullLines.IsValid);
        Assert.False(nullItem.IsValid);
        Assert.False(duplicate.IsValid);
        Assert.False(oversized.IsValid);
    }

    [Fact]
    public void ReserveStockValidatesIdsAndQuantitiesWithoutRejectingPastExpiry()
    {
        var validator = new ReserveStockRequestValidator();
        var invalid = validator.Validate(new ReserveStockRequest(Guid.Empty, DateTimeOffset.MinValue,
            [new StockLineRequest(Guid.Empty, 0)]));
        var retry = validator.Validate(new ReserveStockRequest(Guid.NewGuid(), DateTimeOffset.MinValue,
            [new StockLineRequest(ProductItemId, 1)]));

        Assert.Contains(invalid.Errors, error => error.PropertyName == nameof(ReserveStockRequest.OrderId));
        Assert.Contains(invalid.Errors, error => error.PropertyName.EndsWith(nameof(StockLineRequest.ProductItemId), StringComparison.Ordinal));
        Assert.Contains(invalid.Errors, error => error.PropertyName.EndsWith(nameof(StockLineRequest.Quantity), StringComparison.Ordinal));
        Assert.True(retry.IsValid);
    }

    [Fact]
    public void CreateAdjustmentRejectsInvalidReasonAndLines()
    {
        var validator = new CreateAdjustmentRequestValidator();
        var invalid = validator.Validate(new CreateAdjustmentRequest("not-a-reason", new string('x', 2001),
            [new AdjustmentLineRequest(Guid.Empty, 0)]));
        var duplicate = validator.Validate(new CreateAdjustmentRequest("Damaged", null,
            [new AdjustmentLineRequest(ProductItemId, 1), new AdjustmentLineRequest(ProductItemId, -1)]));
        var nullLines = validator.Validate(new CreateAdjustmentRequest("Damaged", null, null!));
        var nullItem = validator.Validate(new CreateAdjustmentRequest("Damaged", null, [null!]));

        Assert.Contains(invalid.Errors, error => error.PropertyName == nameof(CreateAdjustmentRequest.Reason));
        Assert.Contains(invalid.Errors, error => error.PropertyName == nameof(CreateAdjustmentRequest.Note));
        Assert.Contains(invalid.Errors, error => error.PropertyName.EndsWith(nameof(AdjustmentLineRequest.ProductItemId), StringComparison.Ordinal));
        Assert.Contains(invalid.Errors, error => error.PropertyName.EndsWith(nameof(AdjustmentLineRequest.QuantityDelta), StringComparison.Ordinal));
        Assert.False(duplicate.IsValid);
        Assert.False(nullLines.IsValid);
        Assert.False(nullItem.IsValid);
    }

    [Fact]
    public void CreateAdjustmentAcceptsDefinedReasonIgnoringCase()
    {
        var request = new CreateAdjustmentRequest("damaged", null, [new AdjustmentLineRequest(ProductItemId, -1)]);

        Assert.True(new CreateAdjustmentRequestValidator().Validate(request).IsValid);
    }

    [Fact]
    public void UpdateAdjustmentValidatesConcurrencyStampAndSharedContent()
    {
        var validator = new UpdateAdjustmentRequestValidator();
        var invalid = validator.Validate(new UpdateAdjustmentRequest(" ", "unknown", null, []));
        var tooLong = validator.Validate(new UpdateAdjustmentRequest(new string('x', 65), "Damaged", null,
            [new AdjustmentLineRequest(ProductItemId, 1)]));
        var valid = validator.Validate(new UpdateAdjustmentRequest(new string('x', 64), "DAMAGED", null,
            [new AdjustmentLineRequest(ProductItemId, 1)]));

        Assert.False(invalid.IsValid);
        Assert.False(tooLong.IsValid);
        Assert.True(valid.IsValid);
    }
}
