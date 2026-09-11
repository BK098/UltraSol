using FluentValidation;
using UltraSol.Modules.Catalog.Domain.Catalog.Brands;
using UltraSol.Modules.Catalog.Domain.Catalog.Categories;
using UltraSol.Modules.Catalog.Domain.Catalog.Collections;
using UltraSol.Modules.Catalog.Domain.Abstractions;
using UltraSol.Modules.Catalog.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Collections.Commands;

public sealed record CreateCollectionDto(
    string? Name, 
    string? Description, 
    CollectionType Type,
    IReadOnlyList<Guid>? BrandIds = null, 
    IReadOnlyList<Guid>? CategoryIds = null, 
    RuleMatchMode MatchMode = RuleMatchMode.All);
public sealed record CreateCollectionCommand(CreateCollectionDto? Model) : ICommand<ApiResult<object>>;

public sealed class CreateCollectionValidator : AbstractValidator<CreateCollectionCommand>
{
    public CreateCollectionValidator()
    {
        RuleFor(x => x.Model).NotNull();
        RuleFor(x => x.Model!.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("Name is required.");
        RuleFor(x => x.Model!.Type).IsInEnum();
        RuleFor(x => x.Model!.MatchMode).IsInEnum();
        RuleForEach(x => x.Model!.BrandIds!)
            .NotEmpty()
            .When(x => x.Model!.BrandIds is not null);
        RuleForEach(x => x.Model!.CategoryIds!)
            .NotEmpty()
            .When(x => x.Model!.CategoryIds is not null);
        RuleFor(x => x.Model)
            .Must(model => (model!.BrandIds?.Count ?? 0) + (model.CategoryIds?.Count ?? 0) == 0)
            .WithMessage("Manual collections cannot have automatic rules.")
            .When(x => x.Model!.Type == CollectionType.Manual);
        RuleFor(x => x.Model)
            .Must(model => (model!.BrandIds?.Count ?? 0) + (model.CategoryIds?.Count ?? 0) > 0)
            .WithMessage("Automatic collections require a brand or category rule.")
            .When(x => x.Model!.Type == CollectionType.Automatic);
    }
}

internal sealed class CreateCollectionCommandHandler(ICollectionRepository collections, IBrandRepository brands,
    ICategoryRepository categories, ICatalogUnitOfWork unitOfWork) : ICommandHandler<CreateCollectionCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(CreateCollectionCommand request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var model = request.Model!;
            Collection collection;
            if (model.Type == CollectionType.Manual)
            {
                collection = Collection.CreateManual(model.Name!, model.Description);
            }
            else
            {
                var ruleBrands = new List<Brand>();
                var ruleCategories = new List<Category>();
                foreach (var id in (model.BrandIds ?? []).Distinct())
                {
                    ruleBrands.Add(await brands.GetRequiredByIdAsync(id, ct));
                }
                foreach (var id in (model.CategoryIds ?? []).Distinct())
                {
                    ruleCategories.Add(await categories.GetRequiredByIdAsync(id, ct));
                }
                collection = Collection.CreateAutomatic(model.Name!, ruleBrands, ruleCategories, model.MatchMode, model.Description);
            }
            await collections.AddAsync(collection, ct);
            return ApiResultBuilder.Success<object>(collection.Id, statusCode: 201);
        }, cancellationToken);
}