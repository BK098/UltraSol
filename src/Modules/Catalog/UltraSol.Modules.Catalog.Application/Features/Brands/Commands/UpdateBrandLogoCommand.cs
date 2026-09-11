using FluentValidation;
using UltraSol.Modules.Catalog.Domain.Abstractions;
using UltraSol.Modules.Catalog.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Brands.Commands;

public sealed record UpdateBrandLogoDto(string? LogoUrl);
public sealed record UpdateBrandLogoCommand(Guid BrandId, UpdateBrandLogoDto? Model) : ICommand<ApiResult<object>>;
public sealed class UpdateBrandLogoValidator : AbstractValidator<UpdateBrandLogoCommand>
{
    public UpdateBrandLogoValidator()
    {
        RuleFor(x => x.BrandId)
            .NotEmpty();
        RuleFor(x => x.Model)
            .NotNull();
        RuleFor(x => x.Model!.LogoUrl)
            .Must(url => !string.IsNullOrWhiteSpace(url))
            .WithMessage("LogoUrl is required.")
            .When(x => x.Model is not null);
    }
}
internal sealed class UpdateBrandLogoCommandHandler(IBrandRepository brands, ICatalogUnitOfWork unitOfWork)
    : ICommandHandler<UpdateBrandLogoCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(UpdateBrandLogoCommand request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var brand = await brands.GetTrackedRequiredAsync(request.BrandId, ct);
            brand.UpdateLogo(request.Model!.LogoUrl!);
            return ApiResultBuilder.Success<object>(brand.Id);
        }, cancellationToken);
}
