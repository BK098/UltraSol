using FluentValidation;
using UltraSol.Modules.Pricing.Application.Common;
using UltraSol.Modules.Pricing.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Pricing.Application.Features.Contracts.Commands;

public sealed record TerminateContractCommand(Guid Id) : ICommand<ApiResult<object>>;

public sealed class TerminateContractValidator : AbstractValidator<TerminateContractCommand>
{
    public TerminateContractValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

internal sealed class TerminateContractHandler(IContractRepository contracts, IPricingUnitOfWork unitOfWork, TimeProvider clock)
    : ICommandHandler<TerminateContractCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(TerminateContractCommand request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var contract = await contracts.GetLockedRequiredAsync(request.Id, true, ct);
            contract.Terminate(null, PricingTime.Normalize(clock.GetUtcNow()));
            return ApiResultBuilder.Success<object>(contract.Id);
        }, cancellationToken);
}