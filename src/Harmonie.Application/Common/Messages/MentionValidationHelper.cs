using Harmonie.Application.Common;
using Harmonie.Application.Interfaces.Users;
using Harmonie.Domain.Common;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Common.Messages;

/// <summary>
/// Shared mention validation logic used by both <see cref="MessageSendOrchestrator"/>
/// and <see cref="MessageEditDeleteOrchestrator"/>.
/// </summary>
public static class MentionValidationHelper
{
    /// <summary>
    /// Validates mentioned user IDs: resolves GUIDs to UserIds, checks existence,
    /// verifies membership via the scope, and returns the validated distinct UserId array.
    /// On failure, the error code and message are set.
    /// </summary>
    public static async Task<MentionValidationResult> ValidateAsync<TContext>(
        IReadOnlyList<Guid> rawMentionedUserIds,
        IUserRepository userRepository,
        Func<IReadOnlyCollection<UserId>, TContext, CancellationToken, Task<Result>> validateMembershipAsync,
        TContext context,
        CancellationToken ct)
    {
        var distinctIds = rawMentionedUserIds.Distinct().ToArray();
        var userIds = distinctIds.Select(UserId.From).ToArray();

        var existingUsers = await userRepository.GetManyByIdsAsync(userIds, ct);
        var existingUserIds = existingUsers.Select(u => u.Id).ToHashSet();
        var missingIds = new List<Guid>();
        foreach (var id in userIds)
        {
            if (!existingUserIds.Contains(id))
                missingIds.Add(id.Value);
        }

        if (missingIds.Count > 0)
        {
            return MentionValidationResult.Failure(
                ApplicationErrorCodes.Message.MentionedUserNotFound,
                $"One or more mentioned users were not found: {string.Join(", ", missingIds)}");
        }

        var validateResult = await validateMembershipAsync(userIds, context, ct);
        if (validateResult.IsFailure)
        {
            return MentionValidationResult.Failure(
                ApplicationErrorCodes.Message.MentionedUserNotMember,
                validateResult.Error ?? "One or more mentioned users are not members of this scope");
        }

        return MentionValidationResult.Success(userIds);
    }
}

/// <summary>
/// Internal result for mention validation. Avoids coupling to the HTTP-flavored
/// <see cref="ApplicationResponse{T}"/> for a pure domain-logic helper.
/// </summary>
public sealed record MentionValidationResult
{
    public bool IsSuccess { get; }
    public UserId[]? Value { get; }
    public string? ErrorCode { get; }
    public string? ErrorMessage { get; }

    private MentionValidationResult(bool isSuccess, UserId[]? value, string? errorCode, string? errorMessage)
    {
        IsSuccess = isSuccess;
        Value = value;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public static MentionValidationResult Success(UserId[] value)
        => new(true, value, null, null);

    public static MentionValidationResult Failure(string errorCode, string errorMessage)
        => new(false, null, errorCode, errorMessage);
}
