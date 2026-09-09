using FluentValidation;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Categories.Commands;

public sealed record ArchiveCategoryDto(string Name);
public sealed record ArchiveCategoryCommand(ArchiveCategoryDto Model) : ICommand<ApiResult<object>>;
public sealed class ArchiveCategoryValidator : AbstractValidator<ArchiveCategoryCommand>
{
    public ArchiveCategoryValidator()
    {
    }
}
internal sealed class ArchiveCategoryCommandHandler : ICommandHandler<ArchiveCategoryCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(ArchiveCategoryCommand request, CancellationToken cancellationToken)
    {
        var model = request.Model;
        throw new NotImplementedException();
    }
}