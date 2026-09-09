using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Categories.Commands;

public sealed record DeactivateCategoryDto(string Name);
public sealed record DeactivateCategoryCommand(DeactivateCategoryDto Model) : ICommand<ApiResult<object>>;
public sealed class DeactivateCategoryValidator : AbstractValidator<DeactivateCategoryCommand>
{
    public DeactivateCategoryValidator()
    {
    }
}
internal sealed class DeactivateCategoryCommandHandler : ICommandHandler<DeactivateCategoryCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(DeactivateCategoryCommand request, CancellationToken cancellationToken)
    {
        var model = request.Model;
        throw new NotImplementedException();
    }
}