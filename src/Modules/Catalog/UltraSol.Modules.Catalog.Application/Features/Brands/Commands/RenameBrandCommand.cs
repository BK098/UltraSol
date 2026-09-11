using FluentValidation;
using UltraSol.Modules.Catalog.Domain.Abstractions;
using UltraSol.Modules.Catalog.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Brands.Commands;

public sealed record RenameBrandDto(string? Name);
public sealed record RenameBrandCommand(Guid BrandId, RenameBrandDto? Model) : ICommand<ApiResult<object>>;
public sealed class RenameBrandValidator : AbstractValidator<RenameBrandCommand>
{
    public RenameBrandValidator()
    {
        RuleFor(x => x.BrandId)
            .NotEmpty();
        RuleFor(x => x.Model)
            .NotNull();
        RuleFor(x => x.Model!.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("Name is required.")
            .When(x => x.Model is not null);
    }
}
internal sealed class RenameBrandCommandHandler(IBrandRepository brands, ICatalogUnitOfWork unitOfWork)
    : ICommandHandler<RenameBrandCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(RenameBrandCommand request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var brand = await brands.GetTrackedRequiredAsync(request.BrandId, ct);
            brand.Rename(request.Model!.Name!);
            return ApiResultBuilder.Success<object>(brand.Id);
        }, cancellationToken);
}
