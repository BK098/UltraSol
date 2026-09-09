using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Categories.Commands;

public sealed record MoveCategoryDto(string Name);
public sealed record MoveCategoryCommand(MoveCategoryDto Model) : ICommand<ApiResult<object>>;
public sealed class MoveCategoryValidator : AbstractValidator<MoveCategoryCommand>
{
    public MoveCategoryValidator()
    {
    }
}
internal sealed class MoveCategoryCommandHandler : ICommandHandler<MoveCategoryCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(MoveCategoryCommand request, CancellationToken cancellationToken)
    {
        var model = request.Model;
        throw new NotImplementedException();
    }
}