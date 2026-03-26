using System.Globalization;
using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Features.Users;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Features.Conversations.SearchConversationMessages;

public sealed record SearchConversationMessagesInput(ConversationId ConversationId, string? Q = null, string? Before = null, string? After = null, string? Cursor = null, int? Limit = null);

public sealed class SearchConversationMessagesHandler : IAuthenticatedHandler<SearchConversationMessagesInput, SearchConversationMessagesResponse>
{
    private const int DefaultLimit = 25;

    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageSearchRepository _directMessageRepository;

    public SearchConversationMessagesHandler(
        IConversationRepository conversationRepository,
        IMessageSearchRepository directMessageRepository)
    {
        _conversationRepository = conversationRepository;
        _directMessageRepository = directMessageRepository;
    }

    public async Task<ApplicationResponse<SearchConversationMessagesResponse>> HandleAsync(
        SearchConversationMessagesInput request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (request.Q is not string rawQuery || string.IsNullOrWhiteSpace(rawQuery))
        {
            return ApplicationResponse<SearchConversationMessagesResponse>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "Request validation succeeded but search query was missing.");
        }

        MessageCursor? cursor = null;
        if (request.Cursor is not null)
        {
            if (!MessageCursorCodec.TryParse(request.Cursor, out var parsedCursor) || parsedCursor is null)
            {
                return ApplicationResponse<SearchConversationMessagesResponse>.Fail(
                    ApplicationErrorCodes.Common.ValidationFailed,
                    "Request validation failed",
                    EndpointExtensions.SingleValidationError(
                        nameof(request.Cursor),
                        ApplicationErrorCodes.Validation.InvalidFormat,
                        "Cursor is invalid"));
            }

            cursor = parsedCursor;
        }

        DateTime? beforeCreatedAtUtc = null;
        if (request.Before is not null)
        {
            if (!TryParseUtcDateTime(request.Before, out var parsedBefore))
            {
                return ApplicationResponse<SearchConversationMessagesResponse>.Fail(
                    ApplicationErrorCodes.Common.ValidationFailed,
                    "Request validation failed",
                    EndpointExtensions.SingleValidationError(
                        nameof(request.Before),
                        ApplicationErrorCodes.Validation.InvalidFormat,
                        "Before must be a valid ISO 8601 date/time"));
            }

            beforeCreatedAtUtc = parsedBefore;
        }

        DateTime? afterCreatedAtUtc = null;
        if (request.After is not null)
        {
            if (!TryParseUtcDateTime(request.After, out var parsedAfter))
            {
                return ApplicationResponse<SearchConversationMessagesResponse>.Fail(
                    ApplicationErrorCodes.Common.ValidationFailed,
                    "Request validation failed",
                    EndpointExtensions.SingleValidationError(
                        nameof(request.After),
                        ApplicationErrorCodes.Validation.InvalidFormat,
                        "After must be a valid ISO 8601 date/time"));
            }

            afterCreatedAtUtc = parsedAfter;
        }

        if (beforeCreatedAtUtc.HasValue
            && afterCreatedAtUtc.HasValue
            && afterCreatedAtUtc.Value > beforeCreatedAtUtc.Value)
        {
            return ApplicationResponse<SearchConversationMessagesResponse>.Fail(
                ApplicationErrorCodes.Common.ValidationFailed,
                "Request validation failed",
                EndpointExtensions.SingleValidationError(
                    nameof(request.After),
                    ApplicationErrorCodes.Validation.OutOfRange,
                    "After must be earlier than or equal to before"));
        }

        var access = await _conversationRepository.GetByIdWithParticipantCheckAsync(request.ConversationId, currentUserId, cancellationToken);
        if (access is null)
        {
            return ApplicationResponse<SearchConversationMessagesResponse>.Fail(
                ApplicationErrorCodes.Conversation.NotFound,
                "Conversation was not found");
        }
        if (!access.IsParticipant)
        {
            return ApplicationResponse<SearchConversationMessagesResponse>.Fail(
                ApplicationErrorCodes.Conversation.AccessDenied,
                "You do not have access to this conversation");
        }

        var limit = request.Limit ?? DefaultLimit;
        var page = await _directMessageRepository.SearchConversationMessagesAsync(
            new SearchConversationMessagesQuery(
                ConversationId: request.ConversationId,
                SearchText: rawQuery.Trim(),
                BeforeCreatedAtUtc: beforeCreatedAtUtc,
                AfterCreatedAtUtc: afterCreatedAtUtc,
                Cursor: cursor),
            limit,
            cancellationToken);

        var payload = new SearchConversationMessagesResponse(
            ConversationId: request.ConversationId.ToString(),
            Items: page.Items
                .Select(item =>
                {
                    var authorAvatar = item.AuthorAvatarColor is not null || item.AuthorAvatarIcon is not null || item.AuthorAvatarBg is not null
                        ? new AvatarAppearanceDto(item.AuthorAvatarColor, item.AuthorAvatarIcon, item.AuthorAvatarBg)
                        : null;

                    return new SearchConversationMessagesItemResponse(
                        MessageId: item.MessageId.ToString(),
                        AuthorUserId: item.AuthorUserId.ToString(),
                        AuthorUsername: item.AuthorUsername,
                        AuthorDisplayName: item.AuthorDisplayName,
                        AuthorAvatarFileId: item.AuthorAvatarFileId?.ToString(),
                        AuthorAvatar: authorAvatar,
                        Content: item.Content.Value,
                        Attachments: item.Attachments.Select(MessageAttachmentDto.FromDomain).ToArray(),
                        CreatedAtUtc: item.CreatedAtUtc,
                        UpdatedAtUtc: item.UpdatedAtUtc);
                })
                .ToArray(),
            NextCursor: page.NextCursor is null ? null : MessageCursorCodec.Encode(page.NextCursor));

        return ApplicationResponse<SearchConversationMessagesResponse>.Ok(payload);
    }

    private static bool TryParseUtcDateTime(string input, out DateTime value)
    {
        value = default;

        if (!DateTimeOffset.TryParse(
                input,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            return false;
        }

        value = parsed.UtcDateTime;
        return true;
    }
}
