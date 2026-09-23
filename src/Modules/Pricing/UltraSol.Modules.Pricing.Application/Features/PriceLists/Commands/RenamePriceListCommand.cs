using FluentValidation;
using UltraSol.Modules.Pricing.Application.Common;
using UltraSol.Modules.Pricing.Application.Features.PriceLists.Models;
using UltraSol.Modules.Pricing.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Pricing.Application.Features.PriceLists.Commands;

public sealed record RenamePriceListCommand(Guid Id, RenamePriceListDto? Model) : ICommand<ApiResult<object>>;

public sealed class RenamePriceListValidator : AbstractValidator<RenamePriceListCommand>
{
    public RenamePriceListValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Model).NotNull();
        RuleFor(x => x.Model!.Name).NotEmpty().MaximumLength(250).When(x => x.Model is not null);
    }
}

internal sealed class RenamePriceListHandler(IPriceListRepository lists, IPricingUnitOfWork unitOfWork, TimeProvider clock)
    : ICommandHandler<RenamePriceListCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(RenamePriceListCommand request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var list = await lists.GetTrackedRequiredAsync(request.Id, ct);
            list.Rename(request.Model!.Name!);
            list.MarkUpdated(null, PricingTime.Normalize(clock.GetUtcNow()));
            return ApiResultBuilder.Success<object>(list.Id);
        }, cancellationToken);
}