using FluentValidation;
using UltraSol.Modules.Organization.Application.Features.Departments;
using UltraSol.Modules.Organization.Application.Messaging;
using UltraSol.Modules.Organization.Domain;
using UltraSol.Modules.Organization.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.IntegrationEvents.Organization;

namespace UltraSol.Modules.Organization.Application.Features.Employees.Commands;

public sealed record UpdateEmployeeCommand(Guid EmployeeId, Guid DepartmentId, bool IsActive) : ICommand<ApiResult<object>>;
public sealed class UpdateEmployeeValidator : AbstractValidator<UpdateEmployeeCommand>
{
    public UpdateEmployeeValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.DepartmentId).NotEmpty();
    }
}
internal sealed class UpdateEmployeeCommandHandler(IEmployeeRepository employees, IDepartmentRepository departments, IOrganizationOutbox outbox, IOrganizationUnitOfWork unitOfWork) : ICommandHandler<UpdateEmployeeCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(UpdateEmployeeCommand request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            if (!await departments.AnyAsync(value => value.Id == request.DepartmentId && value.IsActive, ct))
            {
                return ApiResultBuilder.Error<object>("Department is not active", 400);
            }
            var employee = await employees.GetTrackedRequiredAsync(request.EmployeeId, ct);
            employee.Update(request.DepartmentId, request.IsActive);
            outbox.Add(new EmployeeAccessChanged(Guid.NewGuid(), employee.CorrelationId, employee.Version, 1, employee.Id, employee.UserId!.Value, employee.IsActive));
            return ApiResultBuilder.Success<object>(employee);
        }, cancellationToken);
}