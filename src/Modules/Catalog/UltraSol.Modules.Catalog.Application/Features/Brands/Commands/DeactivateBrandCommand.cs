using FluentValidation;
using UltraSol.Modules.Catalog.Domain.Abstractions;
using UltraSol.Modules.Catalog.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Brands.Commands;

public sealed record DeactivateBrandCommand(Guid BrandId) : ICommand<ApiResult<object>>;
public sealed class DeactivateBrandValidator : AbstractValidator<DeactivateBrandCommand>
{
    public DeactivateBrandValidator()
    {
        RuleFor(x => x.BrandId)
            .NotEmpty();
    }
}
internal sealed class DeactivateBrandCommandHandler(IBrandRepository brands, ICatalogUnitOfWork unitOfWork)
    : ICommandHandler<DeactivateBrandCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(DeactivateBrandCommand request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var brand = await brands.GetTrackedRequiredAsync(request.BrandId, ct);
            brand.Deactivate();
            return ApiResultBuilder.Success<object>(brand.Id);
        }, cancellationToken);
}
