using FluentValidation;
using UltraSol.Modules.Pricing.Application.Common;
using UltraSol.Modules.Pricing.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Pricing.Application.Features.Contracts.Commands;

public sealed record ActivateContractCommand(Guid Id) : ICommand<ApiResult<object>>;

public sealed class ActivateContractValidator : AbstractValidator<ActivateContractCommand>
{
    public ActivateContractValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

internal sealed class ActivateContractHandler(IContractRepository contracts, IPricingUnitOfWork unitOfWork, TimeProvider clock)
    : ICommandHandler<ActivateContractCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(ActivateContractCommand request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var contract = await contracts.GetLockedRequiredAsync(request.Id, true, ct);
            contract.Activate(null, PricingTime.Normalize(clock.GetUtcNow()));
            return ApiResultBuilder.Success<object>(contract.Id);
        }, cancellationToken);
}