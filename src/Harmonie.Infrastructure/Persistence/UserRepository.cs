using Dapper;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.ValueObjects;
using Harmonie.Infrastructure.Rows;

namespace Harmonie.Infrastructure.Persistence;

public sealed class UserRepository : IUserRepository
{
    private const string SelectUserSql = """
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
                                         FROM users
                                         """;

    private readonly DbSession _dbSession;

    public UserRepository(DbSession dbSession)
    {
        _dbSession = dbSession;
    }

    public async Task<User?> GetByIdAsync(UserId userId, CancellationToken ct = default)
    {
        var sql = $"{SelectUserSql} WHERE id = @Id AND deleted_at IS NULL";
        var conn = await _dbSession.GetOpenConnectionAsync(ct);
        var cmd = new CommandDefinition(
            sql,
            new { Id = userId.Value },
            transaction: _dbSession.Transaction,
            cancellationToken: ct);
        var userRow = await conn.QueryFirstOrDefaultAsync<UserRow>(cmd);

        return userRow is null ? null : MapToUser(userRow);
    }

    public async Task<User?> GetByEmailAsync(Email email, CancellationToken ct = default)
    {
        var sql = $"{SelectUserSql} WHERE email = @Email AND deleted_at IS NULL";
        var conn = await _dbSession.GetOpenConnectionAsync(ct);
        var cmd = new CommandDefinition(
            sql,
            new { Email = email.Value },
            transaction: _dbSession.Transaction,
            cancellationToken: ct);
        var userRow = await conn.QueryFirstOrDefaultAsync<UserRow>(cmd);

        return userRow is null ? null : MapToUser(userRow);
    }

    public async Task<User?> GetByUsernameAsync(Username username, CancellationToken ct = default)
    {
        var sql = $"{SelectUserSql} WHERE username = @Username AND deleted_at IS NULL";
        var conn = await _dbSession.GetOpenConnectionAsync(ct);
        var cmd = new CommandDefinition(
            sql,
            new { Username = username.Value },
            transaction: _dbSession.Transaction,
            cancellationToken: ct);
        var userRow = await conn.QueryFirstOrDefaultAsync<UserRow>(cmd);

        return userRow is null ? null : MapToUser(userRow);
    }

    public async Task<bool> ExistsByEmailAsync(Email email, CancellationToken ct = default)
    {
        const string sql = "SELECT EXISTS(SELECT 1 FROM users WHERE email = @Email AND deleted_at IS NULL)";
        var conn = await _dbSession.GetOpenConnectionAsync(ct);
        var cmd = new CommandDefinition(
            sql,
            new { Email = email.Value },
            transaction: _dbSession.Transaction,
            cancellationToken: ct);
        return await conn.ExecuteScalarAsync<bool>(cmd);
    }

    public async Task<bool> ExistsByUsernameAsync(Username username, CancellationToken ct = default)
    {
        const string sql = "SELECT EXISTS(SELECT 1 FROM users WHERE username = @Username AND deleted_at IS NULL)";
        var conn = await _dbSession.GetOpenConnectionAsync(ct);
        var cmd = new CommandDefinition(
            sql,
            new { Username = username.Value },
            transaction: _dbSession.Transaction,
            cancellationToken: ct);
        return await conn.ExecuteScalarAsync<bool>(cmd);
    }

    public async Task AddAsync(User user, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO users (id, email, username, password_hash, avatar_url, is_email_verified,
                is_active, display_name, bio, created_at_utc, updated_at_utc, last_login_at_utc)
            VALUES (@Id, @Email, @Username, @PasswordHash, @AvatarUrl, @IsEmailVerified,
                @IsActive, @DisplayName, @Bio, @CreatedAtUtc, @UpdatedAtUtc, @LastLoginAtUtc)";

        var conn = await _dbSession.GetOpenConnectionAsync(ct);
        var cmd = new CommandDefinition(
            sql,
            new
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
            },
            transaction: _dbSession.Transaction,
            cancellationToken: ct);

        await conn.ExecuteAsync(cmd);
    }

    public async Task UpdateAsync(User user, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE users SET email = @Email, username = @Username, password_hash = @PasswordHash,
                avatar_url = @AvatarUrl, is_email_verified = @IsEmailVerified, is_active = @IsActive,
                display_name = @DisplayName, bio = @Bio, updated_at_utc = @UpdatedAtUtc, last_login_at_utc = @LastLoginAtUtc
            WHERE id = @Id";

        var conn = await _dbSession.GetOpenConnectionAsync(ct);
        var cmd = new CommandDefinition(
            sql,
            new
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
            },
            transaction: _dbSession.Transaction,
            cancellationToken: ct);

        await conn.ExecuteAsync(cmd);
    }

    public async Task UpdateProfileAsync(
        UserId userId,
        bool displayNameIsSet,
        string? displayName,
        bool bioIsSet,
        string? bio,
        bool avatarUrlIsSet,
        string? avatarUrl,
        DateTime? updatedAtUtc,
        CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE users
            SET display_name = CASE WHEN @DisplayNameIsSet THEN @DisplayName ELSE display_name END,
                bio = CASE WHEN @BioIsSet THEN @Bio ELSE bio END,
                avatar_url = CASE WHEN @AvatarUrlIsSet THEN @AvatarUrl ELSE avatar_url END,
                updated_at_utc = @UpdatedAtUtc
            WHERE id = @Id
              AND deleted_at IS NULL";

        var conn = await _dbSession.GetOpenConnectionAsync(ct);
        var cmd = new CommandDefinition(
            sql,
            new
            {
                Id = userId.Value,
                DisplayNameIsSet = displayNameIsSet,
                DisplayName = displayName,
                BioIsSet = bioIsSet,
                Bio = bio,
                AvatarUrlIsSet = avatarUrlIsSet,
                AvatarUrl = avatarUrl,
                UpdatedAtUtc = updatedAtUtc
            },
            transaction: _dbSession.Transaction,
            cancellationToken: ct);

        await conn.ExecuteAsync(cmd);
    }

    public async Task DeleteAsync(UserId userId, CancellationToken ct = default)
    {
        const string sql = "UPDATE users SET deleted_at = @DeletedAt WHERE id = @Id";
        var conn = await _dbSession.GetOpenConnectionAsync(ct);
        var cmd = new CommandDefinition(
            sql,
            new { Id = userId.Value, DeletedAt = DateTime.UtcNow },
            transaction: _dbSession.Transaction,
            cancellationToken: ct);
        await conn.ExecuteAsync(cmd);
    }

    private static User MapToUser(UserRow userRow)
    {
        var emailResult = Email.Create(userRow.Email);
        if (emailResult.IsFailure || emailResult.Value is null)
            throw new InvalidOperationException("Stored email is invalid.");

        var usernameResult = Username.Create(userRow.Username);
        if (usernameResult.IsFailure || usernameResult.Value is null)
            throw new InvalidOperationException("Stored username is invalid.");

        return User.Rehydrate(
            UserId.From(userRow.Id),
            emailResult.Value,
            usernameResult.Value,
            userRow.PasswordHash,
            userRow.AvatarUrl,
            userRow.IsEmailVerified,
            userRow.IsActive,
            userRow.LastLoginAtUtc,
            userRow.DisplayName,
            userRow.Bio,
            userRow.CreatedAtUtc,
            userRow.UpdatedAtUtc);
    }
}
