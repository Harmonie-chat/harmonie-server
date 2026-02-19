using System.Data;
using Dapper;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.ValueObjects;
using Harmonie.Infrastructure.Dto;
using Npgsql;

namespace Harmonie.Infrastructure.Persistence;
public sealed class UserRepository : IUserRepository
{
    private readonly string _connectionString;
    public UserRepository(string connectionString) => _connectionString = connectionString;
    private IDbConnection CreateConnection() => new NpgsqlConnection(_connectionString);
    
    public async Task<User?> GetByIdAsync(UserId userId, CancellationToken ct = default)
    {
        const string sql = """
                           SELECT id as "Id",
                                        email as "Email",
                                        username as "Username",
                                        password_hash as "PasswordHash",
                                        avatar_url as "AvatarUrl",
                                        is_email_verified as "IsEmailVerified",
                                       is_active as "IsActive",
                                       display_name as "DisplayName",
                                       bio as "Bio",
                                       created_at_utc as "CreatedAtUtc",
                                       updated_at_utc as "UpdatedAtUtc",
                                       last_login_at_utc as "LastLoginAtUtc",
                                       deleted_at as "DeletedAt" 
                           FROM users WHERE email = @Email AND deleted_at IS NULL
                           """;
        using var conn = CreateConnection();
        var userRow = await conn.QueryFirstOrDefaultAsync<UserDto>(sql, new { Id = userId.Value });
        if (userRow is null) return null;
        
        return User.Rehydrate(
            UserId.From(userRow.Id),
            Email.Create(userRow.Email).Value!,
            Username.Create(userRow.Username).Value!,
            userRow.PasswordHash,
            userRow.AvatarUrl,
            userRow.IsEmailVerified,
            userRow.IsActive,
            userRow.LastLoginAtUtc,
            userRow.DisplayName,
            userRow.Bio,
            userRow.CreatedAtUtc,
            userRow.UpdatedAtUtc
        );
    }
    
    public async Task<User?> GetByEmailAsync(Email email, CancellationToken ct = default)
    {
            const string sql = """
                               SELECT id as "Id",
                                            email as "Email",
                                            username as "Username",
                                            password_hash as "PasswordHash",
                                            avatar_url as "AvatarUrl",
                                            is_email_verified as "IsEmailVerified",
                                           is_active as "IsActive",
                                           display_name as "DisplayName",
                                           bio as "Bio",
                                           created_at_utc as "CreatedAtUtc",
                                           updated_at_utc as "UpdatedAtUtc",
                                           last_login_at_utc as "LastLoginAtUtc",
                                           deleted_at as "DeletedAt" 
                               FROM users WHERE email = @Email AND deleted_at IS NULL
                               """;
            using var conn = CreateConnection();
            var userRow = await conn.QueryFirstOrDefaultAsync<UserDto>(sql, new { Email = email.Value });
            
            if (userRow is null) return null;
            
            return User.Rehydrate(
                UserId.From(userRow.Id),
                Email.Create(userRow.Email).Value!,
                Username.Create(userRow.Username).Value!,
                userRow.PasswordHash,
                userRow.AvatarUrl,
                userRow.IsEmailVerified,
                userRow.IsActive,
                userRow.LastLoginAtUtc,
                userRow.DisplayName,
                userRow.Bio,
                userRow.CreatedAtUtc,
                userRow.UpdatedAtUtc
            );
    }
    
    public async Task<User?> GetByUsernameAsync(Username username, CancellationToken ct = default)
    {
        const string sql = """
                           SELECT id as "Id",
                                        email as "Email",
                                        username as "Username",
                                        password_hash as "PasswordHash",
                                        avatar_url as "AvatarUrl",
                                        is_email_verified as "IsEmailVerified",
                                       is_active as "IsActive",
                                       display_name as "DisplayName",
                                       bio as "Bio",
                                       created_at_utc as "CreatedAtUtc",
                                       updated_at_utc as "UpdatedAtUtc",
                                       last_login_at_utc as "LastLoginAtUtc",
                                       deleted_at as "DeletedAt" 
                           FROM users WHERE email = @Email AND deleted_at IS NULL
                           """;
        using var conn = CreateConnection();
        var userRow = await conn.QueryFirstOrDefaultAsync<User>(sql, new { Username = username.Value });
        
        
        if (userRow is null) return null;
            
        return User.Rehydrate(
            UserId.From(userRow.Id),
            Email.Create(userRow.Email).Value!,
            Username.Create(userRow.Username).Value!,
            userRow.PasswordHash,
            userRow.AvatarUrl,
            userRow.IsEmailVerified,
            userRow.IsActive,
            userRow.LastLoginAtUtc,
            userRow.DisplayName,
            userRow.Bio,
            userRow.CreatedAtUtc,
            userRow.UpdatedAtUtc
        );
    }
    
    public async Task<bool> ExistsByEmailAsync(Email email, CancellationToken ct = default)
    {
        const string sql = "SELECT EXISTS(SELECT 1 FROM users WHERE email = @Email AND deleted_at IS NULL)";
        using var conn = CreateConnection();
        return await conn.ExecuteScalarAsync<bool>(sql, new { Email = email.Value });
    }
    
    public async Task<bool> ExistsByUsernameAsync(Username username, CancellationToken ct = default)
    {
        const string sql = "SELECT EXISTS(SELECT 1 FROM users WHERE username = @Username AND deleted_at IS NULL)";
        using var conn = CreateConnection();
        return await conn.ExecuteScalarAsync<bool>(sql, new { Username = username.Value });
    }
    
    public async Task AddAsync(User user, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO users (id, email, username, password_hash, avatar_url, is_email_verified, 
                is_active, display_name, bio, created_at_utc, updated_at_utc, last_login_at_utc)
            VALUES (@Id, @Email, @Username, @PasswordHash, @AvatarUrl, @IsEmailVerified, 
                @IsActive, @DisplayName, @Bio, @CreatedAtUtc, @UpdatedAtUtc, @LastLoginAtUtc)";
        using var conn = CreateConnection();
        await conn.ExecuteAsync(sql, new
        {
            Id = user.Id.Value,
            Email = user.Email.Value,
            Username = user.Username.Value,
            user.PasswordHash,
            user.AvatarUrl,
            user.IsEmailVerified,
            user.IsActive,
            user.DisplayName,
            user.Bio,
            user.CreatedAtUtc,
            user.UpdatedAtUtc,
            user.LastLoginAtUtc
        });
    }
    
    public async Task UpdateAsync(User user, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE users SET email = @Email, username = @Username, password_hash = @PasswordHash,
                avatar_url = @AvatarUrl, is_email_verified = @IsEmailVerified, is_active = @IsActive,
                display_name = @DisplayName, bio = @Bio, updated_at_utc = @UpdatedAtUtc, last_login_at_utc = @LastLoginAtUtc
            WHERE id = @Id";
        using var conn = CreateConnection();
        await conn.ExecuteAsync(sql, new
        {
            Id = user.Id.Value,
            Email = user.Email.Value,
            Username = user.Username.Value,
            user.PasswordHash,
            user.AvatarUrl,
            user.IsEmailVerified,
            user.IsActive,
            user.DisplayName,
            user.Bio,
            user.UpdatedAtUtc,
            user.LastLoginAtUtc
        });
    }
    
    public async Task DeleteAsync(UserId userId, CancellationToken ct = default)
    {
        const string sql = "UPDATE users SET deleted_at = @DeletedAt WHERE id = @Id";
        using var conn = CreateConnection();
        await conn.ExecuteAsync(sql, new { Id = userId.Value, DeletedAt = DateTime.UtcNow });
    }
}
