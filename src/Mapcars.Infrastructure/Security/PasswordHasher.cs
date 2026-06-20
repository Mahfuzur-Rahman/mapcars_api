using Mapcars.Application.Common.Interfaces;
using BC = BCrypt.Net.BCrypt;

namespace Mapcars.Infrastructure.Security;

public class PasswordHasher : IPasswordHasher
{
    public string Hash(string password) => BC.HashPassword(password, workFactor: 12);
    public bool Verify(string password, string hash) => BC.Verify(password, hash);
}
