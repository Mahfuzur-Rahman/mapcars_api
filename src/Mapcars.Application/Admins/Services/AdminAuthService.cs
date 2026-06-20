using Mapcars.Application.Admins.Dtos;
using Mapcars.Application.Admins.Interfaces;
using Mapcars.Application.Admins.Mapping;
using Mapcars.Application.Common.Exceptions;
using Mapcars.Application.Common.Interfaces;
using Mapcars.Domain.Entities;
using Mapcars.Domain.Exceptions;

namespace Mapcars.Application.Admins.Services;

// Input validation runs at the API boundary (ValidationActionFilter); this
// service only enforces business rules (uniqueness, account state, setup guard).
public class AdminAuthService(
    IAdminRepository adminRepo,
    IJwtService jwtService,
    IPasswordHasher hasher,
    IUnitOfWork uow) : IAdminAuthService
{
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var admin = await adminRepo.GetByEmailAsync(request.Email, ct)
            ?? throw new UnauthorizedException("Invalid email or password.");

        if (!admin.IsActive)
            throw new UnauthorizedException("This account has been disabled.");

        if (!hasher.Verify(request.Password, admin.PasswordHash))
            throw new UnauthorizedException("Invalid email or password.");

        var menus = await adminRepo.GetMenusForAdminAsync(admin.Id, admin.RoleId, ct);

        return new LoginResponse
        {
            Token = jwtService.GenerateToken(admin),
            ExpiresInMinutes = jwtService.ExpiryMinutes,
            Admin = admin.ToResponse(),
            Menus = menus.ToMenuTree(),
        };
    }

    public async Task<AdminResponse> RegisterAsync(CreateAdminRequest request, Guid createdBy, CancellationToken ct = default)
    {
        if (await adminRepo.EmailExistsAsync(request.Email, ct))
            throw new DomainException("An admin with this email already exists.");

        var admin = NewAdmin(request, createdBy);
        await adminRepo.AddAsync(admin, ct);
        await uow.SaveChangesAsync(ct);

        // Reload with role for the response
        var saved = await adminRepo.GetByIdWithRoleAsync(admin.Id, ct) ?? admin;
        return saved.ToResponse();
    }

    public async Task<LoginResponse> SetupSuperAdminAsync(CreateAdminRequest request, CancellationToken ct = default)
    {
        if (await adminRepo.AnyAdminsExistAsync(ct))
            throw new DomainException("Setup already complete. Use /login instead.");

        var superAdminRequest = new CreateAdminRequest
        {
            Email = request.Email,
            Password = request.Password,
            FullName = request.FullName,
            RoleId = 1, // SuperAdmin — always forced on initial setup
        };
        var admin = NewAdmin(superAdminRequest, createdBy: null);
        await adminRepo.AddAsync(admin, ct);
        await uow.SaveChangesAsync(ct);

        var saved = await adminRepo.GetByIdWithRoleAsync(admin.Id, ct) ?? admin;
        var menus = await adminRepo.GetMenusForAdminAsync(saved.Id, saved.RoleId, ct);

        return new LoginResponse
        {
            Token = jwtService.GenerateToken(saved),
            ExpiresInMinutes = jwtService.ExpiryMinutes,
            Admin = saved.ToResponse(),
            Menus = menus.ToMenuTree(),
        };
    }

    public async Task<LoginResponse> GetCurrentAdminAsync(Guid adminId, CancellationToken ct = default)
    {
        var admin = await adminRepo.GetByIdWithRoleAsync(adminId, ct)
            ?? throw new NotFoundException("Admin", adminId);

        var menus = await adminRepo.GetMenusForAdminAsync(admin.Id, admin.RoleId, ct);

        return new LoginResponse
        {
            Token = jwtService.GenerateToken(admin),
            ExpiresInMinutes = jwtService.ExpiryMinutes,
            Admin = admin.ToResponse(),
            Menus = menus.ToMenuTree(),
        };
    }

    private Admin NewAdmin(CreateAdminRequest req, Guid? createdBy) => new()
    {
        Id = Guid.NewGuid(),
        Email = req.Email.ToLowerInvariant().Trim(),
        PasswordHash = hasher.Hash(req.Password),
        FullName = req.FullName.Trim(),
        RoleId = req.RoleId,
        IsActive = true,
        CreatedBy = createdBy,
        CreatedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = DateTime.UtcNow,
    };
}
