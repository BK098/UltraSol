using FluentValidation;
using UltraSol.Modules.Catalog.Domain.Abstractions;
using UltraSol.Modules.Catalog.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Brands.Commands;

public sealed record ActivateBrandCommand(Guid BrandId) : ICommand<ApiResult<object>>;
public sealed class ActivateBrandValidator : AbstractValidator<ActivateBrandCommand>
{
    public ActivateBrandValidator()
    {
        RuleFor(x => x.BrandId)
            .NotEmpty();
    }
}
internal sealed class ActivateBrandCommandHandler(IBrandRepository brands, ICatalogUnitOfWork unitOfWork)
    : ICommandHandler<ActivateBrandCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(ActivateBrandCommand request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var brand = await brands.GetTrackedRequiredAsync(request.BrandId, ct);
            brand.Activate();
            return ApiResultBuilder.Success<object>(brand.Id);
        }, cancellationToken);
}
