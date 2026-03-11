using System.Globalization;
using Harmonie.Application.Common;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Conversations.SearchConversationMessages;

public sealed class SearchConversationMessagesHandler
{
    private const int DefaultLimit = 25;

    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageRepository _directMessageRepository;
    private readonly ILogger<SearchConversationMessagesHandler> _logger;

    public SearchConversationMessagesHandler(
        IConversationRepository conversationRepository,
        IMessageRepository directMessageRepository,
        ILogger<SearchConversationMessagesHandler> logger)
    {
        _conversationRepository = conversationRepository;
        _directMessageRepository = directMessageRepository;
        _logger = logger;
    }

    public async Task<ApplicationResponse<SearchConversationMessagesResponse>> HandleAsync(
        ConversationId conversationId,
        SearchConversationMessagesRequest request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "SearchConversationMessages started. ConversationId={ConversationId}, UserId={UserId}, Limit={Limit}, HasCursor={HasCursor}",
            conversationId,
            currentUserId,
            request.Limit ?? DefaultLimit,
            request.Cursor is not null);

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

        var conversation = await _conversationRepository.GetByIdAsync(conversationId, cancellationToken);
        if (conversation is null)
        {
            _logger.LogWarning(
                "SearchConversationMessages failed because conversation was not found. ConversationId={ConversationId}, UserId={UserId}",
                conversationId,
                currentUserId);

            return ApplicationResponse<SearchConversationMessagesResponse>.Fail(
                ApplicationErrorCodes.Conversation.NotFound,
                "Conversation was not found");
        }

        if (conversation.User1Id != currentUserId && conversation.User2Id != currentUserId)
        {
            _logger.LogWarning(
                "SearchConversationMessages access denied. ConversationId={ConversationId}, UserId={UserId}",
                conversationId,
                currentUserId);

            return ApplicationResponse<SearchConversationMessagesResponse>.Fail(
                ApplicationErrorCodes.Conversation.AccessDenied,
                "You do not have access to this conversation");
        }

        var limit = request.Limit ?? DefaultLimit;
        var page = await _directMessageRepository.SearchConversationMessagesAsync(
            new SearchConversationMessagesQuery(
                ConversationId: conversationId,
                SearchText: rawQuery.Trim(),
                BeforeCreatedAtUtc: beforeCreatedAtUtc,
                AfterCreatedAtUtc: afterCreatedAtUtc,
                Cursor: cursor),
            limit,
            cancellationToken);

        _logger.LogInformation(
            "SearchConversationMessages succeeded. ConversationId={ConversationId}, UserId={UserId}, ItemCount={ItemCount}, HasNextCursor={HasNextCursor}",
            conversationId,
            currentUserId,
            page.Items.Count,
            page.NextCursor is not null);

        var payload = new SearchConversationMessagesResponse(
            ConversationId: conversationId.ToString(),
            Items: page.Items
                .Select(item => new SearchConversationMessagesItemResponse(
                    MessageId: item.MessageId.ToString(),
                    AuthorUserId: item.AuthorUserId.ToString(),
                    AuthorUsername: item.AuthorUsername,
                    AuthorDisplayName: item.AuthorDisplayName,
                    AuthorAvatarUrl: item.AuthorAvatarUrl,
                    Content: item.Content.Value,
                    CreatedAtUtc: item.CreatedAtUtc,
                    UpdatedAtUtc: item.UpdatedAtUtc))
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
