using FluentValidation.Results;
using Mapcars.Application.Common.Interfaces;
using Mapcars.Application.Riders.Dtos;
using Mapcars.Application.Riders.Interfaces;
using Mapcars.Application.Riders.Mapping;
using Mapcars.Domain.Entities;
using ValidationException = Mapcars.Application.Common.Exceptions.ValidationException;
using NotFoundException = Mapcars.Application.Common.Exceptions.NotFoundException;

namespace Mapcars.Application.Riders.Services;

/// <summary>
/// Business logic for riders. Input shape validation runs at the API boundary
/// (ValidationActionFilter); this service enforces business rules (uniqueness)
/// and orchestrates the repository + unit of work. Knows nothing about HTTP or EF.
/// </summary>
public class RiderService : IRiderService
{
    private readonly IRiderRepository _riders;
    private readonly IUnitOfWork _uow;

    public RiderService(IRiderRepository riders, IUnitOfWork uow)
    {
        _riders = riders;
        _uow = uow;
    }

    public async Task<RiderResponse> CreateAsync(CreateRiderRequest request, CancellationToken ct = default)
    {
        if (await _riders.EmailExistsAsync(request.Email, ct))
        {
            throw new ValidationException(new[]
            {
                new ValidationFailure(nameof(request.Email), "A rider with this email already exists.")
            });
        }

        var rider = new Rider
        {
            FullName = request.FullName,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber
        };

        await _riders.AddAsync(rider, ct);
        await _uow.SaveChangesAsync(ct);

        return rider.ToResponse();
    }

    public async Task<RiderResponse> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var rider = await _riders.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Rider), id);

        return rider.ToResponse();
    }

    public async Task<IReadOnlyList<RiderResponse>> ListAsync(CancellationToken ct = default)
    {
        var riders = await _riders.ListAsync(ct);
        return riders.Select(r => r.ToResponse()).ToList();
    }
}
