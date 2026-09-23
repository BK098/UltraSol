using FluentValidation.TestHelper;
using UltraSol.Modules.Pricing.Application.Features.Quotes.Commands;
using Xunit;

namespace UltraSol.Modules.Pricing.Tests;

public sealed class QuoteTests
{
    [Fact]
    public void Quote_rejects_duplicate_lines_and_invalid_commercial_context()
    {
        var productItemId = Guid.NewGuid();
        var validator = new CreatePriceQuoteValidator();

        var duplicate = validator.TestValidate(new CreatePriceQuoteCommand(new("Retail", "VND", null, null, null,
            [new(productItemId, 1), new(productItemId, 2)])));
        var invalidContract = validator.TestValidate(new CreatePriceQuoteCommand(new("Contract", "VND", null, null, null,
            [new(productItemId, 1)])));

        duplicate.ShouldHaveValidationErrorFor(command => command.Model!.Lines);
        invalidContract.ShouldHaveValidationErrorFor(command => command.Model!.CustomerId);
        invalidContract.ShouldHaveValidationErrorFor(command => command.Model!.ContractId);
    }

    [Fact]
    public void Quote_accepts_guest_retail_and_caps_batch_size()
    {
        var validator = new CreatePriceQuoteValidator();
        var retail = validator.TestValidate(new CreatePriceQuoteCommand(new("Retail", "vnd", null, null, null,
            [new(Guid.NewGuid(), 1)])));
        var tooMany = validator.TestValidate(new CreatePriceQuoteCommand(new("Wholesale", "VND", null, null, null,
            Enumerable.Range(0, 101).Select(_ => new CreatePriceQuoteCommand.Line(Guid.NewGuid(), 1)).ToArray())));

        retail.ShouldNotHaveAnyValidationErrors();
        tooMany.ShouldHaveValidationErrorFor(command => command.Model!.Lines);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("2")]
    [InlineData("999")]
    public void Quote_rejects_numeric_order_types(string orderType)
    {
        var result = new CreatePriceQuoteValidator().TestValidate(new CreatePriceQuoteCommand(new(orderType, "VND", null, null, null,
            [new(Guid.NewGuid(), 1)])));

        result.ShouldHaveValidationErrorFor(command => command.Model!.OrderType);
    }

    [Fact]
    public void Quote_rejects_null_line_elements_without_throwing()
    {
        var result = new CreatePriceQuoteValidator().TestValidate(new CreatePriceQuoteCommand(new("Retail", "VND", null, null, null,
            [null!])));

        result.ShouldHaveValidationErrorFor("Model.Lines[0]");
    }
}