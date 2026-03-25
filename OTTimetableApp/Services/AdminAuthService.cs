using Microsoft.EntityFrameworkCore;
using OTTimetableApp.Data;
using OTTimetableApp.Data.Models;
using System.Security.Cryptography;

namespace OTTimetableApp.Services;

public class AdminAuthService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private const int Iterations = 100_000;
    private const int HashSize = 32;

    public AdminAuthService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public bool Verify(string username, string password)
    {
        using var db = _dbFactory.CreateDbContext();
        var user = db.AdminUsers.AsNoTracking()
            .FirstOrDefault(u => u.Username == username);

        return user != null && CheckPassword(password, user.PasswordHash);
    }

    // Returns true on success, false if currentPassword is wrong
    public bool ChangePassword(string currentPassword, string newPassword)
    {
        using var db = _dbFactory.CreateDbContext();
        var user = db.AdminUsers.FirstOrDefault();
        if (user == null) return false;

        if (!CheckPassword(currentPassword, user.PasswordHash))
            return false;

        user.PasswordHash = HashPassword(newPassword);
        db.SaveChanges();
        return true;
    }

    public static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(HashSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);

        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    private static bool CheckPassword(string password, string storedHash)
    {
        var parts = storedHash.Split('.');
        if (parts.Length != 3) return false;
        if (!int.TryParse(parts[0], out var iterations)) return false;

        byte[] salt = Convert.FromBase64String(parts[1]);
        byte[] expected = Convert.FromBase64String(parts[2]);
        byte[] actual = Rfc2898DeriveBytes.Pbkdf2(
            password, salt, iterations, HashAlgorithmName.SHA256, HashSize);

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
