using FluentValidation;
using UltraSol.Modules.Catalog.Domain.Abstractions;
using UltraSol.Modules.Catalog.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Variations.Commands;

public sealed record RenameVariationOptionDto(string? Value);
public sealed record RenameVariationOptionCommand(Guid ProductId, Guid VariationId, Guid OptionId, RenameVariationOptionDto? Model) : ICommand<ApiResult<object>>;
public sealed class RenameVariationOptionValidator : AbstractValidator<RenameVariationOptionCommand>
{
    public RenameVariationOptionValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty();
        RuleFor(x => x.VariationId)
            .NotEmpty();
        RuleFor(x => x.OptionId)
            .NotEmpty();
        RuleFor(x => x.Model)
            .NotNull();
        RuleFor(x => x.Model!.Value)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("Value is required.")
            .When(x => x.Model is not null);
    }
}
internal sealed class RenameVariationOptionCommandHandler(IProductRepository products, ICatalogUnitOfWork unitOfWork)
    : ICommandHandler<RenameVariationOptionCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(RenameVariationOptionCommand request, CancellationToken cancellationToken) => unitOfWork.ExecuteInTransactionAsync(async ct =>
    {
        var product = await products.GetTrackedRequiredAsync(request.ProductId, ct);
        product.RenameVariationOption(request.VariationId, request.OptionId, request.Model!.Value!);
        return ApiResultBuilder.Success<object>(request.OptionId);
    }, cancellationToken);
}
