using FluentValidation;
using UltraSol.Modules.Catalog.Application.Reads;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Paging;
namespace UltraSol.Modules.Catalog.Application.Features.Client.Queries;
public sealed record GetClientProductsQuery(PagedFilter Filter) : IQuery<ApiResult<PaginatedResult<GetClientProductsQuery.Response>>>
{
    public sealed record Response(Guid Id, string Name, string? Description, string? Image);
}
public sealed class GetClientProductsValidator : AbstractValidator<GetClientProductsQuery>
{
    public GetClientProductsValidator()
    {
        RuleFor(x => x.Filter).NotNull();
        When(x => x.Filter is not null, () =>
        {
            RuleFor(x => x.Filter.PageIndex).GreaterThan(0);
            RuleFor(x => x.Filter.PageSize).InclusiveBetween(1, PaginationRequest.MaxPageSize);
        });
    }
}
internal sealed class GetClientProductsQueryHandler(IClientCatalogReadStore reads) : IQueryHandler<GetClientProductsQuery, ApiResult<PaginatedResult<GetClientProductsQuery.Response>>>
{
    public async Task<ApiResult<PaginatedResult<GetClientProductsQuery.Response>>> Handle(GetClientProductsQuery request, CancellationToken ct)
    {
        var products = await reads.ProductsAsync(CatalogQueryPaging.Create(request.Filter, "Name"), ct);
        return ApiResultBuilder.Success(products.Map(product => new GetClientProductsQuery.Response(product.Id, product.Name, product.Description, product.Image)));
    }
}