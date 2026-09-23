using FluentValidation;
using UltraSol.Modules.Catalog.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.ProductItems.Queries;

public sealed record SkuExistsQuery(Guid SkuId) : IQuery<ApiResult<bool>>;

public sealed class SkuExistsValidator : AbstractValidator<SkuExistsQuery>
{
    public SkuExistsValidator()
    {
        RuleFor(request => request.SkuId).NotEmpty();
    }
}

internal sealed class SkuExistsQueryHandler(IProductItemRepository items) : IQueryHandler<SkuExistsQuery, ApiResult<bool>>
{
    public async Task<ApiResult<bool>> Handle(SkuExistsQuery request, CancellationToken cancellationToken) =>
        ApiResultBuilder.Success(await items.AnyAsync(item => item.Id == request.SkuId, cancellationToken));
}