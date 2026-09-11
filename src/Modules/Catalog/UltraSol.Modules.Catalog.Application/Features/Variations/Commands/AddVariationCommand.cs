using FluentValidation;
using UltraSol.Modules.Catalog.Domain.Abstractions;
using UltraSol.Modules.Catalog.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Variations.Commands;

public sealed record AddVariationDto(string? Name);
public sealed record AddVariationCommand(Guid ProductId, AddVariationDto? Model) : ICommand<ApiResult<object>>;
public sealed class AddVariationValidator : AbstractValidator<AddVariationCommand>
{
    public AddVariationValidator()
    {
        RuleFor(x => x.Model).NotNull();
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Model!.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("Name is required.")
            .When(x => x.Model is not null);
    }
}
internal sealed class AddVariationCommandHandler(IProductRepository products, ICatalogUnitOfWork unitOfWork)
    : ICommandHandler<AddVariationCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(AddVariationCommand request, CancellationToken cancellationToken) => unitOfWork.ExecuteInTransactionAsync(async ct =>
    {
        var product = await products.GetTrackedRequiredAsync(request.ProductId, ct);
        return ApiResultBuilder.Success<object>(product.AddVariation(request.Model!.Name!));
    }, cancellationToken);
}
