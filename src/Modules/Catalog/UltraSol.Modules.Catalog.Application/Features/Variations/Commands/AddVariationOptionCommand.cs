using FluentValidation;
using UltraSol.Modules.Catalog.Domain.Abstractions;
using UltraSol.Modules.Catalog.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Variations.Commands;

public sealed record AddVariationOptionDto(string? Value);
public sealed record AddVariationOptionCommand(Guid ProductId, Guid VariationId, AddVariationOptionDto? Model) : ICommand<ApiResult<object>>;
public sealed class AddVariationOptionValidator : AbstractValidator<AddVariationOptionCommand>
{
    public AddVariationOptionValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty();
        RuleFor(x => x.VariationId)
            .NotEmpty();
        RuleFor(x => x.Model)
            .NotNull();
        RuleFor(x => x.Model!.Value)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("Value is required.")
            .When(x => x.Model is not null);
    }
}
internal sealed class AddVariationOptionCommandHandler(IProductRepository products, ICatalogUnitOfWork unitOfWork)
    : ICommandHandler<AddVariationOptionCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(AddVariationOptionCommand request, CancellationToken cancellationToken) => unitOfWork.ExecuteInTransactionAsync(async ct =>
    {
        var product = await products.GetTrackedRequiredAsync(request.ProductId, ct);
        return ApiResultBuilder.Success<object>(product.AddVariationOption(request.VariationId, request.Model!.Value!));
    }, cancellationToken);
}
