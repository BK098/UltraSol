using FluentValidation;
using UltraSol.Modules.Pricing.Application.Common;
using UltraSol.Modules.Pricing.Application.Features.PriceLists.Models;
using UltraSol.Modules.Pricing.Domain.PriceLists;
using UltraSol.Modules.Pricing.Domain.Repositories;
using UltraSol.Modules.Pricing.Domain.ValueObjects;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Pricing.Application.Features.PriceLists.Commands;

public sealed record CreatePriceListCommand(CreatePriceListDto? Model) : ICommand<ApiResult<object>>;

public sealed class CreatePriceListValidator : AbstractValidator<CreatePriceListCommand>
{
    public CreatePriceListValidator()
    {
        RuleFor(x => x.Model).NotNull();
        When(x => x.Model is not null, () =>
        {
            RuleFor(x => x.Model!.Name).NotEmpty().MaximumLength(250);
            RuleFor(x => x.Model!.Currency).Must(PricingInput.ValidCurrency).WithMessage("Currency must contain three ASCII letters.");
            RuleFor(x => x.Model!.Type).IsInEnum();
        });
    }
}

internal sealed class CreatePriceListHandler(IPriceListRepository lists, IPricingUnitOfWork unitOfWork, TimeProvider clock)
    : ICommandHandler<CreatePriceListCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(CreatePriceListCommand request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var model = request.Model!;
            var list = PriceList.Create(model.Name!, Currency.Create(model.Currency!), model.Type);
            list.MarkCreated(null, PricingTime.Normalize(clock.GetUtcNow()));
            await lists.AddAsync(list, ct);
            return ApiResultBuilder.Success<object>(list.Id, statusCode: 201);
        }, cancellationToken);
}