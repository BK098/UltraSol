using FluentValidation;
using UltraSol.Modules.Catalog.Application.Reads;
using UltraSol.Modules.Catalog.Domain.Catalog.Collections;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems;
using UltraSol.Modules.Catalog.Domain.Catalog.Products;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.Paging;
using static UltraSol.Modules.Catalog.Application.Features.Bundles.Queries.GetBundleDetailQuery;

namespace UltraSol.Modules.Catalog.Application.Features.Bundles.Queries;

public sealed record GetBundleDetailQuery(Guid ProductId, Guid ProductItemId) : IQuery<ApiResult<GetBundleDetailQuery.Response>>
{
    public sealed record Response(Guid Id, string Sku, string Status, Named Product, bool IsBundle, IReadOnlyList<Selection> Selections,
        IReadOnlyList<Media> Media, IReadOnlyList<Component> Components);
    public sealed record Named(Guid Id, string Name);
    public sealed record Selection(Guid VariationId, string VariationName, Guid OptionId, string OptionValue);
    public sealed record Media(Guid Id, string Url, string? AltText, int SortOrder, bool IsPrimary);
    public sealed record Component(Guid Id, string Sku, Named Product, string Status, int Quantity, string? PrimaryMediaUrl);
}

public sealed class GetBundleDetailValidator : AbstractValidator<GetBundleDetailQuery>
{
    public GetBundleDetailValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty();
        RuleFor(x => x.ProductItemId)
            .NotEmpty();
    }
}

internal sealed class GetBundleDetailQueryHandler(ICatalogReadStore reads) : IQueryHandler<GetBundleDetailQuery, ApiResult<GetBundleDetailQuery.Response>>
{
    public async Task<ApiResult<GetBundleDetailQuery.Response>> Handle(GetBundleDetailQuery request, CancellationToken ct)
    {
        var data = await reads.ItemAsync(request.ProductItemId, request.ProductId, true, ct);
        var row = data;
        return ApiResultBuilder.Success(new Response(row.Id, row.Sku, row.Status.ToString(), new Named(row.Product.Id, row.Product.Name), row.IsBundle,
            row.Selections.Select(v0 => new Selection(v0.VariationId, v0.VariationName, v0.OptionId, v0.OptionValue)).ToArray(),
            row.Media.Select(v0 => new Media(v0.Id, v0.Url, v0.AltText, v0.SortOrder, v0.IsPrimary)).ToArray(),
            row.Components.Select(v0 => new Component(v0.Id, v0.Sku, new Named(v0.Product.Id, v0.Product.Name), v0.Status, v0.Quantity, v0.PrimaryMediaUrl)).ToArray()));
    }
}