using UltraSol.Modules.Catalog.Domain.Repositories;
using FluentValidation;
using Variation = UltraSol.Modules.Catalog.Application.Features.Products.Queries.GetProductDetailQuery.Variation;
using UltraSol.Modules.Catalog.Application.Reads;
using UltraSol.Modules.Catalog.Domain.Catalog.Collections;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems;
using UltraSol.Modules.Catalog.Domain.Catalog.Products;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.Paging;
using static UltraSol.Modules.Catalog.Application.Features.Products.Queries.GetProductDetailQuery;

namespace UltraSol.Modules.Catalog.Application.Features.Products.Queries;

public sealed record GetProductDetailQuery(Guid ProductId) : IQuery<ApiResult<GetProductDetailQuery.Response>>
{
    public sealed record Response(Guid Id, string Name, string? Description, string Status, Named? Brand, IReadOnlyList<Named> Categories,
        IReadOnlyList<Media> Media, IReadOnlyList<Variation> Variations);
    public sealed record Named(Guid Id, string Name);
    public sealed record Media(Guid Id, string Url, string? AltText, int SortOrder, bool IsPrimary);
    public sealed record Variation(Guid Id, string Name, Named Product, int OptionCount, IReadOnlyList<Option> Options);
    public sealed record Option(Guid Id, string Value);
}

public sealed class GetProductDetailValidator : AbstractValidator<GetProductDetailQuery>
{
    public GetProductDetailValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty();
    }
}

internal sealed class GetProductDetailQueryHandler(ICatalogReadStore reads) : IQueryHandler<GetProductDetailQuery, ApiResult<GetProductDetailQuery.Response>>
{
    public async Task<ApiResult<GetProductDetailQuery.Response>> Handle(GetProductDetailQuery request, CancellationToken ct)
    {
        var data = await reads.ProductAsync(request.ProductId, ct);
        var row = data;
        return ApiResultBuilder.Success(new Response(row.Id, row.Name, row.Description, row.Status.ToString(),
            row.Brand is null ? null : new Named(row.Brand.Id, row.Brand.Name), row.Categories.Select(v0 => new Named(v0.Id, v0.Name)).ToArray(),
            row.Media.Select(v0 => new Media(v0.Id, v0.Url, v0.AltText, v0.SortOrder, v0.IsPrimary)).ToArray(),
            row.Variations.Select(v0 => new Variation(v0.Id, v0.Name, new Named(v0.Product.Id, v0.Product.Name), v0.OptionCount,
            v0.Options.Select(v1 => new Option(v1.Id, v1.Value)).ToArray())).ToArray()));
    }
}