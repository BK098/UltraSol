using FluentValidation;
using UltraSol.Modules.Organization.Application.Messaging;
using UltraSol.Modules.Organization.Domain;
using UltraSol.Modules.Organization.Domain.Employees;
using UltraSol.Modules.Organization.Domain.Repositories;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.IntegrationEvents.Organization;

namespace UltraSol.Modules.Organization.Application.Features.Employees.Commands;

public sealed record CreateEmployeeCommand(Guid DepartmentId, string Email) : ICommand<ApiResult<object>>;
public sealed class CreateEmployeeValidator : AbstractValidator<CreateEmployeeCommand>
{
    public CreateEmployeeValidator()
    {
        RuleFor(x => x.DepartmentId).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
    }
}
internal sealed class CreateEmployeeCommandHandler(IEmployeeRepository employees, IDepartmentRepository departments, IOrganizationOutbox outbox, IOrganizationUnitOfWork unitOfWork) : ICommandHandler<CreateEmployeeCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(CreateEmployeeCommand request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            if (!await departments.AnyAsync(value => value.Id == request.DepartmentId && value.IsActive, ct))
            {
                return ApiResultBuilder.Error<object>("Department is not active", 400);
            }
            var employee = new Employee(request.Email, request.DepartmentId);
            await employees.AddAsync(employee, ct);
            outbox.Add(new EmployeeAccountRequested(Guid.NewGuid(), employee.CorrelationId, employee.Version, 1, employee.Id, employee.Email, employee.DepartmentId));
            return ApiResultBuilder.Success<object>(employee);
        }, cancellationToken);
}