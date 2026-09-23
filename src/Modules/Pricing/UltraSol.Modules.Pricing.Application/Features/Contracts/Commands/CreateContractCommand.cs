using FluentValidation;
using UltraSol.Modules.Pricing.Application.Common;
using UltraSol.Modules.Pricing.Domain.Contracts;
using UltraSol.Modules.Pricing.Domain.Repositories;
using UltraSol.Modules.Pricing.Domain.ValueObjects;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Exceptions;

namespace UltraSol.Modules.Pricing.Application.Features.Contracts.Commands;

public sealed record CreateContractDto(Guid CustomerId, Guid PriceListId, DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveTo, string? CommercialTerms);

public sealed record CreateContractCommand(CreateContractDto? Model) : ICommand<ApiResult<object>>;

public sealed class CreateContractValidator : AbstractValidator<CreateContractCommand>
{
    public CreateContractValidator()
    {
        RuleFor(x => x.Model).NotNull();
        When(x => x.Model is not null, () =>
        {
            RuleFor(x => x.Model!.CustomerId).NotEmpty();
            RuleFor(x => x.Model!.PriceListId).NotEmpty();
            RuleFor(x => x.Model!.EffectiveFrom).NotEmpty();
            RuleFor(x => x.Model!).Must(x => x.EffectiveTo is null || PricingTime.Normalize(x.EffectiveTo.Value) > PricingTime.Normalize(x.EffectiveFrom))
                .WithMessage("EffectiveTo must be after EffectiveFrom.");
            RuleFor(x => x.Model!.CommercialTerms).NotEmpty();
        });
    }
}

internal sealed class CreateContractHandler(IPriceListRepository lists, IContractRepository contracts, IPricingUnitOfWork unitOfWork, TimeProvider clock)
    : ICommandHandler<CreateContractCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(CreateContractCommand request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var model = request.Model!;
            var list = await lists.GetRequiredByIdAsync(model.PriceListId, ct);
            if (await contracts.FindByPriceListAsync(list.Id, ct) is not null)
            {
                throw new DomainException("The price list is already assigned to a contract.", "ContractPriceListAlreadyAssigned");
            }
            var validity = EffectivePeriod.Create(PricingTime.Normalize(model.EffectiveFrom),
                model.EffectiveTo is { } end ? PricingTime.Normalize(end) : null);
            var contract = Contract.Create(model.CustomerId, list, validity, model.CommercialTerms!);
            contract.MarkCreated(null, PricingTime.Normalize(clock.GetUtcNow()));
            await contracts.AddAsync(contract, ct);
            return ApiResultBuilder.Success<object>(contract.Id, statusCode: 201);
        }, cancellationToken);
}