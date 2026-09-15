using FluentValidation;
using UltraSol.Modules.Organization.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Exceptions;

namespace UltraSol.Modules.Organization.Application.Features.Departments.Commands;

public sealed record UpdateDepartmentCommand(Guid DepartmentId, string Name, Guid? ParentId, bool IsActive) : ICommand<ApiResult<object>>;
public sealed class UpdateDepartmentValidator : AbstractValidator<UpdateDepartmentCommand>
{
    public UpdateDepartmentValidator()
    {
        RuleFor(x => x.DepartmentId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
    }
}
internal sealed class UpdateDepartmentCommandHandler(IDepartmentRepository departments, IEmployeeRepository employees, IOrganizationUnitOfWork unitOfWork) : ICommandHandler<UpdateDepartmentCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(UpdateDepartmentCommand request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var parentId = request.ParentId;
            var department = await departments.GetTrackedRequiredAsync(request.DepartmentId, ct);
            var visited = new HashSet<Guid> { department.Id };
            while (parentId.HasValue)
            {
                if (!visited.Add(parentId.Value))
                {
                    throw new DomainException("Department hierarchy cannot contain cycles.");
                }
                if (!await departments.AnyAsync(value => value.Id == parentId && value.IsActive, ct))
                {
                    //throw new DomainException("Active Department required.");
                    return ApiResultBuilder.Error<object>("Parent department is not active.", 400);
                }
                parentId = (await departments.GetRequiredByIdAsync(parentId.Value, ct)).ParentId;
            }
            if (!request.IsActive && (await departments.AnyAsync(value => value.ParentId == department.Id && value.IsActive, ct) || await employees.AnyAsync(value => value.DepartmentId == department.Id && value.IsActive, ct)))
            {
                throw new DomainException("Department still has active employees or children.");
            }
            department.Update(request.Name, request.ParentId, request.IsActive);
            return ApiResultBuilder.Success<object>(department.Id);
        }, cancellationToken);
}