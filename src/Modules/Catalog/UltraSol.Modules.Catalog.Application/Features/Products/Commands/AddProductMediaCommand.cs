using FluentValidation;
using UltraSol.Modules.Catalog.Domain.Abstractions;
using UltraSol.Modules.Catalog.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Products.Commands;

public sealed record AddProductMediaDto(string? Url, string? AltText);
public sealed record AddProductMediaCommand(Guid ProductId, AddProductMediaDto? Model) : ICommand<ApiResult<object>>;

public sealed class AddProductMediaValidator : AbstractValidator<AddProductMediaCommand>
{
    public AddProductMediaValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Model).NotNull();
        RuleFor(x => x.Model!.Url).Must(url => !string.IsNullOrWhiteSpace(url)).WithMessage("Url is required.")
            .When(x => x.Model is not null);
    }
}

internal sealed class AddProductMediaCommandHandler(IProductRepository products, ICatalogUnitOfWork unitOfWork)
    : ICommandHandler<AddProductMediaCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(AddProductMediaCommand request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var product = await products.GetTrackedRequiredAsync(request.ProductId, ct);
            var mediaId = product.AddMedia(request.Model!.Url!, request.Model.AltText);
            return ApiResultBuilder.Success<object>(mediaId);
        }, cancellationToken);
}
