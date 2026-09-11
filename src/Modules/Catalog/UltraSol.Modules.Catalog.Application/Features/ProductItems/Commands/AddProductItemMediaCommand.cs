using FluentValidation;
using UltraSol.Modules.Catalog.Domain.Abstractions;
using UltraSol.Modules.Catalog.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.ProductItems.Commands;

public sealed record AddProductItemMediaDto(string? Url, string? AltText);
public sealed record AddProductItemMediaCommand(Guid ProductId, Guid ProductItemId, AddProductItemMediaDto? Model) : ICommand<ApiResult<object>>;
public sealed class AddProductItemMediaValidator : AbstractValidator<AddProductItemMediaCommand>
{
    public AddProductItemMediaValidator()
    {
        RuleFor(x => x.Model).NotNull();
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.ProductItemId).NotEmpty();
        RuleFor(x => x.Model!.Url)
            .Must(url => !string.IsNullOrWhiteSpace(url))
            .WithMessage("Url is required.")
            .When(x => x.Model is not null);
    }
}
internal sealed class AddProductItemMediaCommandHandler(IProductRepository products, IProductItemRepository items, ICatalogUnitOfWork unitOfWork)
    : ICommandHandler<AddProductItemMediaCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(AddProductItemMediaCommand request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var product = await products.GetTrackedRequiredAsync(request.ProductId, ct);
            var item = await items.GetTrackedRequiredAsync(request.ProductItemId, ct);
            return ApiResultBuilder.Success<object>(item.AddMedia(product, request.Model!.Url!, request.Model.AltText));
        }, cancellationToken);
}