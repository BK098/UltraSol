using FluentValidation;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Brands.Commands
{
    public sealed record ArchiveBrandDto(string Name);
    public sealed record ArchiveBrandCommand(ArchiveBrandDto Model) : ICommand<ApiResult<object>>;
    public sealed class ArchiveBrandValidator : AbstractValidator<ArchiveBrandCommand>
    {
        public ArchiveBrandValidator()
        {
        }
    }
    internal sealed class ArchiveBrandCommandHandler : ICommandHandler<ArchiveBrandCommand, ApiResult<object>>
    {
        public Task<ApiResult<object>> Handle(ArchiveBrandCommand request, CancellationToken cancellationToken)
        {
            var model = request.Model;
            throw new NotImplementedException();
        }
    }
}