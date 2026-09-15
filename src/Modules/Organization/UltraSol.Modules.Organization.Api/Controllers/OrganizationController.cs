using MediatR;
using Microsoft.AspNetCore.Mvc;
using UltraSol.Shared.Domain.Common.Paging;
using UltraSol.Shared.Infrastructure.Api;

namespace UltraSol.Modules.Organization.Api.Controllers;

[Route("api/organization")]
internal sealed class OrganizationController(ISender sender) : BaseController(sender)
{
    [HttpGet("departments")]
    public Task<IActionResult> GetDepartments([FromQuery] PagedFilter filter, CancellationToken ct) => 
        SendAsync(new GetDepartmentsQuery(filter), ct);
    [HttpGet("departments/{id}")]
    public Task<IActionResult> GetDepartmentById(Guid id, CancellationToken ct) => 
        SendAsync(new GetDepartmentByIdQuery(id), ct);
    [HttpGet("departments/{id}/tree")]
    public Task<IActionResult> GetDepartmentTreeAsync(Guid id, CancellationToken ct) =>
        SendAsync(new GetDepartmentTreeQuery(id), ct);
    [HttpPost("departments")]
    public Task<IActionResult> CreateDepartment(CreateDepartmentCommand command, CancellationToken ct) => SendAsync(command, ct);
    [HttpPut("departments")]
    public Task<IActionResult> UpdateDepartment(UpdateDepartmentCommand command, CancellationToken ct) => SendAsync(command, ct);
    [HttpGet("employees")]
    public Task<IActionResult> GetEmployees(CancellationToken ct) => SendAsync(new EmployeesQuery(), ct);
    [HttpPost("employees")]
    public Task<IActionResult> CreateEmployee(CreateEmployeeCommand command, CancellationToken ct) => SendAsync(command, ct);
    [HttpPut("employees")]
    public Task<IActionResult> UpdateEmployee(UpdateEmployeeCommand command, CancellationToken ct) => SendAsync(command, ct);
}