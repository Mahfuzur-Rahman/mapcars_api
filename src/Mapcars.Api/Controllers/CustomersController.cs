using Mapcars.Application.Customers.Dtos;
using Mapcars.Application.Customers.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mapcars.Api.Controllers;

/// <summary>
/// Thin presentation layer — translates HTTP to/from the ICustomerService.
/// No business logic here. This is the template for every feature controller.
/// Admin-only: customers sign up/manage their own profile via CustomerAuthController,
/// not this controller — this is the admin-portal read surface (Customer List/Detail).
/// </summary>
[ApiController]
// Literal, not the [controller] token: with the token, renaming the class
// silently moves the public URL with no diff line saying so.
[Route("api/v1/customers")]
// DEPRECATED alias — delete once the admin portal ships the renamed client.
[Route("api/v1/riders")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class CustomersController : ControllerBase
{
    private readonly ICustomerService _customers;

    public CustomersController(ICustomerService customers) => _customers = customers;

    [HttpPost]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CustomerResponse>> Create(
        CreateCustomerRequest request, CancellationToken ct)
    {
        var customer = await _customers.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = customer.Id }, customer);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerResponse>> GetById(Guid id, CancellationToken ct)
        => Ok(await _customers.GetByIdAsync(id, ct));

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CustomerResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CustomerResponse>>> List(CancellationToken ct)
        => Ok(await _customers.ListAsync(ct));
}
