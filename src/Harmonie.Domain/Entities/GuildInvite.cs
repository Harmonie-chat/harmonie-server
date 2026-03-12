using System.Security.Cryptography;
using Harmonie.Domain.Common;
using Harmonie.Domain.ValueObjects;

namespace Harmonie.Domain.Entities;

public sealed class GuildInvite : Entity<GuildInviteId>
{
    private const int CodeLength = 8;
    private const string CodeChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    public string Code { get; private set; }
    public GuildId GuildId { get; private set; }
    public UserId CreatorId { get; private set; }
    public int? MaxUses { get; private set; }
    public int UsesCount { get; private set; }
    public DateTime? ExpiresAtUtc { get; private set; }

    private GuildInvite(
        GuildInviteId id,
        string code,
        GuildId guildId,
        UserId creatorId,
        int? maxUses,
        int usesCount,
        DateTime? expiresAtUtc,
        DateTime createdAtUtc)
    {
        Id = id;
        Code = code;
        GuildId = guildId;
        CreatorId = creatorId;
        MaxUses = maxUses;
        UsesCount = usesCount;
        ExpiresAtUtc = expiresAtUtc;
        CreatedAtUtc = createdAtUtc;
    }

    public static Result<GuildInvite> Create(
        GuildId guildId,
        UserId creatorId,
        int? maxUses,
        int? expiresInHours)
    {
        if (maxUses is <= 0)
            return Result<GuildInvite>.Failure("Max uses must be greater than 0 if provided");

        if (expiresInHours is <= 0)
            return Result<GuildInvite>.Failure("Expiration hours must be greater than 0 if provided");

        var now = DateTime.UtcNow;
        DateTime? expiresAtUtc = expiresInHours.HasValue
            ? now.AddHours(expiresInHours.Value)
            : null;

        var invite = new GuildInvite(
            GuildInviteId.New(),
            GenerateCode(),
            guildId,
            creatorId,
            maxUses,
            usesCount: 0,
            expiresAtUtc,
            createdAtUtc: now);

        return Result<GuildInvite>.Success(invite);
    }

    public static GuildInvite Rehydrate(
        GuildInviteId id,
        string code,
        GuildId guildId,
        UserId creatorId,
        int? maxUses,
        int usesCount,
        DateTime? expiresAtUtc,
        DateTime createdAtUtc)
    {
        return new GuildInvite(id, code, guildId, creatorId, maxUses, usesCount, expiresAtUtc, createdAtUtc);
    }

    private static string GenerateCode()
    {
        return string.Create(CodeLength, (object?)null, static (span, _) =>
        {
            Span<byte> randomBytes = stackalloc byte[CodeLength];
            RandomNumberGenerator.Fill(randomBytes);

            for (var i = 0; i < CodeLength; i++)
                span[i] = CodeChars[randomBytes[i] % CodeChars.Length];
        });
    }
}
