using FluentValidation;
using UltraSol.Modules.Catalog.Application.Reads;
using UltraSol.Modules.Catalog.Domain.Catalog.Collections;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems;
using UltraSol.Modules.Catalog.Domain.Catalog.Products;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.Paging;
using static UltraSol.Modules.Catalog.Application.Features.Variations.Queries.GetVariationOptionDetailQuery;

namespace UltraSol.Modules.Catalog.Application.Features.Variations.Queries;

public sealed record GetVariationOptionDetailQuery(Guid ProductId, Guid VariationId, Guid OptionId) : IQuery<ApiResult<GetVariationOptionDetailQuery.Response>>
{
    public sealed record Response(Guid Id, string Value, VariationReference Variation);
    public sealed record VariationReference(Guid Id, string Name, Named Product);
    public sealed record Named(Guid Id, string Name);
}

public sealed class GetVariationOptionDetailValidator : AbstractValidator<GetVariationOptionDetailQuery>
{
    public GetVariationOptionDetailValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty();
        RuleFor(x => x.VariationId)
            .NotEmpty();
        RuleFor(x => x.OptionId)
            .NotEmpty();
    }
}

internal sealed class GetVariationOptionDetailQueryHandler(ICatalogReadStore reads) : IQueryHandler<GetVariationOptionDetailQuery,
    ApiResult<GetVariationOptionDetailQuery.Response>>
{
    public async Task<ApiResult<GetVariationOptionDetailQuery.Response>> Handle(GetVariationOptionDetailQuery request, CancellationToken ct)
    {
        var owner = await reads.VariationAsync(request.ProductId, request.VariationId, ct);
        var row = owner.Options.SingleOrDefault(x => x.Id == request.OptionId)
            ?? throw EntityNotFoundException.For<VariationOption>(request.OptionId);
        return ApiResultBuilder.Success(new Response(row.Id, row.Value, new VariationReference(owner.Id, owner.Name, new Named(owner.Product.Id, owner.Product.Name))));
    }
}