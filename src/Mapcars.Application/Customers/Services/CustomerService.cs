using FluentValidation.Results;
using Mapcars.Application.Common.Interfaces;
using Mapcars.Application.Customers.Dtos;
using Mapcars.Application.Customers.Interfaces;
using Mapcars.Application.Customers.Mapping;
using Mapcars.Domain.Entities;
using ValidationException = Mapcars.Application.Common.Exceptions.ValidationException;
using NotFoundException = Mapcars.Application.Common.Exceptions.NotFoundException;

namespace Mapcars.Application.Customers.Services;

/// <summary>
/// Business logic for customers. Input shape validation runs at the API boundary
/// (ValidationActionFilter); this service enforces business rules (uniqueness)
/// and orchestrates the repository + unit of work. Knows nothing about HTTP or EF.
/// </summary>
public class CustomerService : ICustomerService
{
    private readonly ICustomerRepository _customers;
    private readonly IUnitOfWork _uow;

    public CustomerService(ICustomerRepository customers, IUnitOfWork uow)
    {
        _customers = customers;
        _uow = uow;
    }

    public async Task<CustomerResponse> CreateAsync(CreateCustomerRequest request, CancellationToken ct = default)
    {
        if (await _customers.EmailExistsAsync(request.Email, ct))
        {
            throw new ValidationException(new[]
            {
                new ValidationFailure(nameof(request.Email), "A customer with this email already exists.")
            });
        }

        var customer = new Customer
        {
            FullName = request.FullName,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber
        };

        await _customers.AddAsync(customer, ct);
        await _uow.SaveChangesAsync(ct);

        return customer.ToResponse();
    }

    public async Task<CustomerResponse> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var customer = await _customers.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Customer), id);

        return customer.ToResponse();
    }

    public async Task<IReadOnlyList<CustomerResponse>> ListAsync(CancellationToken ct = default)
    {
        var customers = await _customers.ListAsync(ct);
        return customers.Select(r => r.ToResponse()).ToList();
    }
}
