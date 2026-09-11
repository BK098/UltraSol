using FluentValidation;
using UltraSol.Modules.Catalog.Domain.Abstractions;
using UltraSol.Modules.Catalog.Domain.Catalog.Brands;
using UltraSol.Modules.Catalog.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Brands.Commands;

public sealed record CreateBrandDto(string? Name, string? Description);
public sealed record CreateBrandCommand(CreateBrandDto? Model) : ICommand<ApiResult<object>>;
public sealed class CreateBrandValidator : AbstractValidator<CreateBrandCommand>
{
    public CreateBrandValidator()
    {
        RuleFor(x => x.Model)
            .NotNull();
        RuleFor(x => x.Model!.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("Name is required.")
            .When(x => x.Model is not null);
    }
}
internal sealed class CreateBrandCommandHandler(IBrandRepository brands, ICatalogUnitOfWork unitOfWork)
    : ICommandHandler<CreateBrandCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(CreateBrandCommand request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var brand = Brand.Create(request.Model!.Name!, request.Model.Description);
            await brands.AddAsync(brand, ct);
            return ApiResultBuilder.Success<object>(brand.Id, statusCode: 201);
        }, cancellationToken);
}
