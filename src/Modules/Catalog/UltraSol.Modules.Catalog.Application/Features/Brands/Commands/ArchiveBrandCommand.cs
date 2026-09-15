using FluentValidation;
using UltraSol.Modules.Catalog.Domain.Abstractions;
using UltraSol.Modules.Catalog.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Brands.Commands;

public sealed record ArchiveBrandCommand(Guid BrandId) : ICommand<ApiResult<object>>;
public sealed class ArchiveBrandValidator : AbstractValidator<ArchiveBrandCommand>
{
    public ArchiveBrandValidator()
    {
        RuleFor(x => x.BrandId)
            .NotEmpty();
    }
}
internal sealed class ArchiveBrandCommandHandler(IBrandRepository brands, ICatalogUnitOfWork unitOfWork)
    : ICommandHandler<ArchiveBrandCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(ArchiveBrandCommand request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var brand = await brands.GetTrackedRequiredAsync(request.BrandId, ct);
            brand.Archive();
            return ApiResultBuilder.Success<object>(brand.Id);
        }, cancellationToken);
}