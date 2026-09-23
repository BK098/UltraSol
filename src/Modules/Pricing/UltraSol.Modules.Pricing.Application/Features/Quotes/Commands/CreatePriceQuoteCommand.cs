using FluentValidation;
using UltraSol.Modules.Pricing.Application.Common;
using UltraSol.Modules.Pricing.Application.Features.Quotes.Services;
using UltraSol.Modules.Pricing.Domain.PriceLists;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Exceptions;

namespace UltraSol.Modules.Pricing.Application.Features.Quotes.Commands;

public sealed record CreatePriceQuoteCommand(CreatePriceQuoteCommand.Request? Model)
    : ICommand<ApiResult<CreatePriceQuoteCommand.Response>>
{
    public sealed record Request(string? OrderType, string? Currency, Guid? CustomerId, Guid? ContractId,
        Guid? NegotiationTransactionRef, Line[]? Lines);
    public sealed record Line(Guid ProductItemId, int Quantity, Guid? NegotiatedPriceId = null);
    public sealed record Response(Guid QuoteId, DateTimeOffset QuotedAt, string Currency, IReadOnlyList<QuotedLine> Lines,
        decimal Subtotal, decimal DiscountTotal, decimal TaxTotal, decimal ShippingAmount, decimal GrandTotal);
    public sealed record QuotedLine(Guid ProductItemId, int Quantity, decimal ListUnitPrice, decimal FinalUnitPrice,
        decimal DiscountAmount, decimal LineTotal, string PriceSource, Guid? PriceListId, Guid? SkuPriceId,
        Guid? PricePeriodId, Guid? ContractPriceAmendmentId, Guid? NegotiatedPriceId, Guid? ContractId);
}

public sealed class CreatePriceQuoteValidator : AbstractValidator<CreatePriceQuoteCommand>
{
    public CreatePriceQuoteValidator()
    {
        RuleFor(command => command.Model).NotNull();
        When(command => command.Model is not null, () =>
        {
            RuleFor(command => command.Model!.OrderType).Must(value => value is not null &&
                Enum.GetNames<PriceListType>().Contains(value, StringComparer.OrdinalIgnoreCase))
                .WithMessage("OrderType must be Retail, Wholesale or Contract.");
            RuleFor(command => command.Model!.Currency).Must(PricingInput.ValidCurrency)
                .WithMessage("Currency must contain three ASCII letters.");
            RuleFor(command => command.Model!.CustomerId).NotEqual(Guid.Empty);
            RuleFor(command => command.Model!.ContractId).NotEqual(Guid.Empty);
            RuleFor(command => command.Model!.NegotiationTransactionRef).NotEqual(Guid.Empty);
            RuleFor(command => command.Model!.Lines).NotNull().NotEmpty().Must(lines => lines is null || lines.Length <= 100)
                .WithMessage("A quote can contain at most 100 lines.")
                .Must(lines => lines is null || lines.Where(line => line is not null)
                    .Select(line => line.ProductItemId).Distinct().Count() == lines.Length)
                .WithMessage("Product item IDs must be unique.");
            RuleForEach(command => command.Model!.Lines).NotNull().ChildRules(line =>
            {
                line.RuleFor(value => value.ProductItemId).NotEmpty();
                line.RuleFor(value => value.Quantity).GreaterThan(0);
                line.RuleFor(value => value.NegotiatedPriceId).NotEqual(Guid.Empty);
            });
            When(command => string.Equals(command.Model!.OrderType, nameof(PriceListType.Contract), StringComparison.OrdinalIgnoreCase), () =>
            {
                RuleFor(command => command.Model!.CustomerId).NotNull();
                RuleFor(command => command.Model!.ContractId).NotNull();
            });
            When(command => !string.Equals(command.Model!.OrderType, nameof(PriceListType.Contract), StringComparison.OrdinalIgnoreCase), () =>
                RuleFor(command => command.Model!.ContractId).Null());
            When(command => command.Model!.Lines?.Any(line => line?.NegotiatedPriceId is not null) == true, () =>
            {
                RuleFor(command => command.Model!.CustomerId).NotNull();
                RuleFor(command => command.Model!.NegotiationTransactionRef).NotNull();
                RuleFor(command => command.Model!.OrderType).NotEqual(nameof(PriceListType.Retail), StringComparer.OrdinalIgnoreCase);
            });
        });
    }
}

internal sealed class CreatePriceQuoteHandler(PricingQuoteService quotes)
    : ICommandHandler<CreatePriceQuoteCommand, ApiResult<CreatePriceQuoteCommand.Response>>
{
    public async Task<ApiResult<CreatePriceQuoteCommand.Response>> Handle(CreatePriceQuoteCommand request, CancellationToken ct)
    {
        try
        {
            return ApiResultBuilder.Success(await quotes.CreateAsync(request.Model!, ct));
        }
        catch (DomainException exception)
        {
            return ApiResultBuilder.Error<CreatePriceQuoteCommand.Response>(exception.Message,
                exception is EntityNotFoundException ? 404 : 409,
                new Dictionary<string, string[]> { ["Code"] = [exception.Code ?? "PricingError"] });
        }
    }
}