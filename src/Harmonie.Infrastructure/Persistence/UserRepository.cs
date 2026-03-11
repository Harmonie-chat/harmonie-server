using System.Text;
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
                                                avatar_file_id as "AvatarFileId",
                                                is_email_verified as "IsEmailVerified",
                                                is_active as "IsActive",
                                                display_name as "DisplayName",
                                                bio as "Bio",
                                                avatar_color as "AvatarColor",
                                                avatar_icon as "AvatarIcon",
                                                avatar_bg as "AvatarBg",
                                                theme as "Theme",
                                                language as "Language",
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

    public async Task<UserDuplicateCheck> CheckDuplicatesAsync(Email email, Username username, CancellationToken ct = default)
    {
        const string sql = """
                           SELECT EXISTS(SELECT 1 FROM users WHERE email = @Email AND deleted_at IS NULL) AS "EmailExists",
                                  EXISTS(SELECT 1 FROM users WHERE username = @Username AND deleted_at IS NULL) AS "UsernameExists"
                           """;
        var conn = await _dbSession.GetOpenConnectionAsync(ct);
        var cmd = new CommandDefinition(
            sql,
            new { Email = email.Value, Username = username.Value },
            transaction: _dbSession.Transaction,
            cancellationToken: ct);
        return await conn.QueryFirstAsync<UserDuplicateCheck>(cmd);
    }

    public async Task<IReadOnlyList<SearchUserResult>> SearchUsersAsync(
        SearchUsersQuery query,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.Limit <= 0)
            throw new ArgumentOutOfRangeException(nameof(query.Limit), "Limit must be positive.");

        var conn = await _dbSession.GetOpenConnectionAsync(ct);
        var parameters = new DynamicParameters();
        parameters.Add("PrefixPattern", $"{query.SearchText}%");
        parameters.Add("ContainsPattern", $"%{query.SearchText}%");
        parameters.Add("Limit", query.Limit);

        var sqlBuilder = new StringBuilder(
            """
            SELECT u.id AS "UserId",
                   u.username AS "Username",
                   u.display_name AS "DisplayName",
                   u.avatar_file_id AS "AvatarFileId",
                   u.is_active AS "IsActive"
            FROM users u
            """);
        sqlBuilder.AppendLine();

        if (query.GuildId is not null)
        {
            sqlBuilder.AppendLine("INNER JOIN guild_members gm ON gm.user_id = u.id AND gm.guild_id = @GuildId");
            parameters.Add("GuildId", query.GuildId.Value);
        }

        sqlBuilder.AppendLine(
            """
            WHERE u.deleted_at IS NULL
              AND u.is_active = TRUE
              AND (
                  u.username ILIKE @PrefixPattern
                  OR COALESCE(u.display_name, '') ILIKE @PrefixPattern
                  OR u.username ILIKE @ContainsPattern
                  OR COALESCE(u.display_name, '') ILIKE @ContainsPattern)
            ORDER BY CASE
                         WHEN u.username ILIKE @PrefixPattern THEN 0
                         WHEN COALESCE(u.display_name, '') ILIKE @PrefixPattern THEN 1
                         WHEN u.username ILIKE @ContainsPattern THEN 2
                         WHEN COALESCE(u.display_name, '') ILIKE @ContainsPattern THEN 3
                         ELSE 4
                     END,
                     u.username ASC,
                     u.id ASC
            LIMIT @Limit
            """);

        var sql = sqlBuilder.ToString();

        var cmd = new CommandDefinition(
            sql,
            parameters,
            transaction: _dbSession.Transaction,
            cancellationToken: ct);

        var rows = await conn.QueryAsync<SearchUserRow>(cmd);
        return rows.Select(MapToSearchUserResult).ToArray();
    }

    public async Task AddAsync(User user, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO users (id, email, username, password_hash, avatar_url, avatar_file_id, is_email_verified,
                is_active, display_name, bio, avatar_color, avatar_icon, avatar_bg, theme, language,
                created_at_utc, updated_at_utc, last_login_at_utc)
            VALUES (@Id, @Email, @Username, @PasswordHash, NULL, @AvatarFileId, @IsEmailVerified,
                @IsActive, @DisplayName, @Bio, @AvatarColor, @AvatarIcon, @AvatarBg, @Theme, @Language,
                @CreatedAtUtc, @UpdatedAtUtc, @LastLoginAtUtc)";

        var conn = await _dbSession.GetOpenConnectionAsync(ct);
        var cmd = new CommandDefinition(
            sql,
            new
            {
                Id = user.Id.Value,
                Email = user.Email.Value,
                Username = user.Username.Value,
                user.PasswordHash,
                AvatarFileId = user.AvatarFileId?.Value,
                user.IsEmailVerified,
                user.IsActive,
                user.DisplayName,
                user.Bio,
                user.AvatarColor,
                user.AvatarIcon,
                user.AvatarBg,
                user.Theme,
                user.Language,
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
                avatar_url = NULL, avatar_file_id = @AvatarFileId, is_email_verified = @IsEmailVerified, is_active = @IsActive,
                display_name = @DisplayName, bio = @Bio,
                avatar_color = @AvatarColor, avatar_icon = @AvatarIcon, avatar_bg = @AvatarBg,
                theme = @Theme, language = @Language,
                updated_at_utc = @UpdatedAtUtc, last_login_at_utc = @LastLoginAtUtc
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
                AvatarFileId = user.AvatarFileId?.Value,
                user.IsEmailVerified,
                user.IsActive,
                user.DisplayName,
                user.Bio,
                user.AvatarColor,
                user.AvatarIcon,
                user.AvatarBg,
                user.Theme,
                user.Language,
                user.UpdatedAtUtc,
                user.LastLoginAtUtc
            },
            transaction: _dbSession.Transaction,
            cancellationToken: ct);

        await conn.ExecuteAsync(cmd);
    }

    public async Task UpdateProfileAsync(
        ProfileUpdateParameters parameters,
        CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE users
            SET display_name = CASE WHEN @DisplayNameIsSet THEN @DisplayName ELSE display_name END,
                bio = CASE WHEN @BioIsSet THEN @Bio ELSE bio END,
                avatar_url = CASE WHEN @AvatarFileIdIsSet THEN NULL ELSE avatar_url END,
                avatar_file_id = CASE WHEN @AvatarFileIdIsSet THEN @AvatarFileId ELSE avatar_file_id END,
                avatar_color = CASE WHEN @AvatarColorIsSet THEN @AvatarColor ELSE avatar_color END,
                avatar_icon = CASE WHEN @AvatarIconIsSet THEN @AvatarIcon ELSE avatar_icon END,
                avatar_bg = CASE WHEN @AvatarBgIsSet THEN @AvatarBg ELSE avatar_bg END,
                theme = CASE WHEN @ThemeIsSet THEN @Theme ELSE theme END,
                language = CASE WHEN @LanguageIsSet THEN @Language ELSE language END,
                updated_at_utc = @UpdatedAtUtc
            WHERE id = @Id
              AND deleted_at IS NULL";

        var conn = await _dbSession.GetOpenConnectionAsync(ct);
        var cmd = new CommandDefinition(
            sql,
            new
            {
                Id = parameters.UserId.Value,
                parameters.DisplayNameIsSet,
                parameters.DisplayName,
                parameters.BioIsSet,
                parameters.Bio,
                parameters.AvatarFileIdIsSet,
                AvatarFileId = parameters.AvatarFileId?.Value,
                parameters.AvatarColorIsSet,
                parameters.AvatarColor,
                parameters.AvatarIconIsSet,
                parameters.AvatarIcon,
                parameters.AvatarBgIsSet,
                parameters.AvatarBg,
                parameters.ThemeIsSet,
                parameters.Theme,
                parameters.LanguageIsSet,
                parameters.Language,
                parameters.UpdatedAtUtc
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
            userRow.AvatarFileId.HasValue ? UploadedFileId.From(userRow.AvatarFileId.Value) : null,
            userRow.IsEmailVerified,
            userRow.IsActive,
            userRow.LastLoginAtUtc,
            userRow.DisplayName,
            userRow.Bio,
            userRow.AvatarColor,
            userRow.AvatarIcon,
            userRow.AvatarBg,
            userRow.Theme,
            userRow.Language,
            userRow.CreatedAtUtc,
            userRow.UpdatedAtUtc);
    }

    private static SearchUserResult MapToSearchUserResult(SearchUserRow row)
    {
        var usernameResult = Username.Create(row.Username);
        if (usernameResult.IsFailure || usernameResult.Value is null)
            throw new InvalidOperationException("Stored search username is invalid.");

        return new SearchUserResult(
            UserId: UserId.From(row.UserId),
            Username: usernameResult.Value,
            DisplayName: row.DisplayName,
            AvatarFileId: row.AvatarFileId.HasValue ? UploadedFileId.From(row.AvatarFileId.Value) : null,
            IsActive: row.IsActive);
    }
}
