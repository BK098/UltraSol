using UltraSol.Modules.Catalog.Domain.Catalog.Products;
using FluentValidation;
using UltraSol.Modules.Catalog.Application.Reads;
using UltraSol.Modules.Catalog.Domain.Catalog.Collections;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.Paging;
using static UltraSol.Modules.Catalog.Application.Features.Variations.Queries.GetVariationDetailQuery;

namespace UltraSol.Modules.Catalog.Application.Features.Variations.Queries;

public sealed record GetVariationDetailQuery(Guid ProductId, Guid VariationId) : IQuery<ApiResult<GetVariationDetailQuery.Response>>
{
    public sealed record Response(Guid Id, string Name, Named Product, IReadOnlyList<Option> Options);
    public sealed record Named(Guid Id, string Name);
    public sealed record Option(Guid Id, string Value);
}

public sealed class GetVariationDetailValidator : AbstractValidator<GetVariationDetailQuery>
{
    public GetVariationDetailValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty();
        RuleFor(x => x.VariationId)
            .NotEmpty();
    }
}

internal sealed class GetVariationDetailQueryHandler(ICatalogReadStore reads) : IQueryHandler<GetVariationDetailQuery, ApiResult<GetVariationDetailQuery.Response>>
{
    public async Task<ApiResult<GetVariationDetailQuery.Response>> Handle(GetVariationDetailQuery request, CancellationToken ct)
    {
        var data = await reads.VariationAsync(request.ProductId, request.VariationId, ct);
        var row = data;
        return ApiResultBuilder.Success(new Response(row.Id, row.Name, new Named(row.Product.Id, row.Product.Name),
            row.Options.Select(v0 => new Option(v0.Id, v0.Value)).ToArray()));
    }
}