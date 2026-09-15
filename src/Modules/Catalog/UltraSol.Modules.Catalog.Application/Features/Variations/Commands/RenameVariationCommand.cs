using FluentValidation;
using UltraSol.Modules.Catalog.Domain.Abstractions;
using UltraSol.Modules.Catalog.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Variations.Commands;

public sealed record RenameVariationDto(string? Name);
public sealed record RenameVariationCommand(Guid ProductId, Guid VariationId, RenameVariationDto? Model) : ICommand<ApiResult<object>>;
public sealed class RenameVariationValidator : AbstractValidator<RenameVariationCommand>
{
    public RenameVariationValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty();
        RuleFor(x => x.VariationId)
            .NotEmpty();
        RuleFor(x => x.Model)
            .NotNull();
        RuleFor(x => x.Model!.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("Name is required.")
            .When(x => x.Model is not null);
    }
}
internal sealed class RenameVariationCommandHandler(IProductRepository products, ICatalogUnitOfWork unitOfWork)
    : ICommandHandler<RenameVariationCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(RenameVariationCommand request, CancellationToken cancellationToken) => unitOfWork.ExecuteInTransactionAsync(async ct =>
    {
        var product = await products.GetTrackedRequiredAsync(request.ProductId, ct);
        product.RenameVariation(request.VariationId, request.Model!.Name!);
        return ApiResultBuilder.Success<object>(request.VariationId);
    }, cancellationToken);
}