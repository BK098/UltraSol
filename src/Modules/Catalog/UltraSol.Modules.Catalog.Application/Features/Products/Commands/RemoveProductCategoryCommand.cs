using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Products.Commands;

public sealed record RemoveProductCategoryDto(string Name);
public sealed record RemoveProductCategoryCommand(RemoveProductCategoryDto Model) : ICommand<ApiResult<object>>;
public sealed class RemoveProductCategoryValidator : AbstractValidator<RemoveProductCategoryCommand>
{
    public RemoveProductCategoryValidator()
    {
    }
}
internal sealed class RemoveProductCategoryCommandHandler : ICommandHandler<RemoveProductCategoryCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(RemoveProductCategoryCommand request, CancellationToken cancellationToken)
    {
        var model = request.Model;
        throw new NotImplementedException();
    }
}