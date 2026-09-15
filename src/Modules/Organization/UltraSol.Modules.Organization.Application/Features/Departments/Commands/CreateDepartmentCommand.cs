using FluentValidation;
using UltraSol.Modules.Organization.Domain;
using UltraSol.Modules.Organization.Domain.Departments;
using UltraSol.Modules.Organization.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Organization.Application.Features.Departments.Commands;

public sealed record CreateDepartmentCommand(string Name, Guid? ParentId = null) : ICommand<ApiResult<object>>;
public sealed class CreateDepartmentValidator : AbstractValidator<CreateDepartmentCommand>
{
    public CreateDepartmentValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
    }
}
internal sealed class CreateDepartmentCommandHandler(IDepartmentRepository departments, IOrganizationUnitOfWork unitOfWork) : ICommandHandler<CreateDepartmentCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(CreateDepartmentCommand request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            if (request.ParentId.HasValue && !await departments.AnyAsync(value => value.Id == request.ParentId && value.IsActive, ct))
            {
                return ApiResultBuilder.Error<object>("Parent department is not active", 400);
            }
            var department = new Department(request.Name, request.ParentId);
            await departments.AddAsync(department, ct);
            return ApiResultBuilder.Success<object>(department.Id);
        }, cancellationToken);
}