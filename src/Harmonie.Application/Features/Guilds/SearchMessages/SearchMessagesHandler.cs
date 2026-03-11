using System.Globalization;
using Harmonie.Application.Common;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Guilds.SearchMessages;

public sealed class SearchMessagesHandler
{
    private const int DefaultLimit = 25;

    private readonly IGuildRepository _guildRepository;
    private readonly IGuildChannelRepository _guildChannelRepository;
    private readonly IMessageRepository _channelMessageRepository;
    private readonly ILogger<SearchMessagesHandler> _logger;

    public SearchMessagesHandler(
        IGuildRepository guildRepository,
        IGuildChannelRepository guildChannelRepository,
        IMessageRepository channelMessageRepository,
        ILogger<SearchMessagesHandler> logger)
    {
        _guildRepository = guildRepository;
        _guildChannelRepository = guildChannelRepository;
        _channelMessageRepository = channelMessageRepository;
        _logger = logger;
    }

    public async Task<ApplicationResponse<SearchMessagesResponse>> HandleAsync(
        GuildId guildId,
        SearchMessagesRequest request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "SearchMessages started. GuildId={GuildId}, UserId={UserId}, Limit={Limit}, HasChannelFilter={HasChannelFilter}, HasAuthorFilter={HasAuthorFilter}, HasCursor={HasCursor}",
            guildId,
            currentUserId,
            request.Limit ?? DefaultLimit,
            request.ChannelId is not null,
            request.AuthorId is not null,
            request.Cursor is not null);

        if (request.Q is not string rawQuery || string.IsNullOrWhiteSpace(rawQuery))
        {
            return ApplicationResponse<SearchMessagesResponse>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "Request validation succeeded but search query was missing.");
        }

        MessageCursor? cursor = null;
        if (request.Cursor is not null)
        {
            if (!MessageCursorCodec.TryParse(request.Cursor, out var parsedCursor) || parsedCursor is null)
            {
                return ApplicationResponse<SearchMessagesResponse>.Fail(
                    ApplicationErrorCodes.Common.ValidationFailed,
                    "Request validation failed",
                    EndpointExtensions.SingleValidationError(
                        nameof(request.Cursor),
                        ApplicationErrorCodes.Validation.InvalidFormat,
                        "Cursor is invalid"));
            }

            cursor = parsedCursor;
        }

        GuildChannelId? channelId = null;
        if (request.ChannelId is not null)
        {
            if (!GuildChannelId.TryParse(request.ChannelId, out var parsedChannelId) || parsedChannelId is null)
            {
                return ApplicationResponse<SearchMessagesResponse>.Fail(
                    ApplicationErrorCodes.Common.ValidationFailed,
                    "Request validation failed",
                    EndpointExtensions.SingleValidationError(
                        nameof(request.ChannelId),
                        ApplicationErrorCodes.Validation.InvalidFormat,
                        "Channel ID is invalid"));
            }

            channelId = parsedChannelId;
        }

        UserId? authorId = null;
        if (request.AuthorId is not null)
        {
            if (!UserId.TryParse(request.AuthorId, out var parsedAuthorId) || parsedAuthorId is null)
            {
                return ApplicationResponse<SearchMessagesResponse>.Fail(
                    ApplicationErrorCodes.Common.ValidationFailed,
                    "Request validation failed",
                    EndpointExtensions.SingleValidationError(
                        nameof(request.AuthorId),
                        ApplicationErrorCodes.Validation.InvalidFormat,
                        "Author ID is invalid"));
            }

            authorId = parsedAuthorId;
        }

        DateTime? beforeCreatedAtUtc = null;
        if (request.Before is not null)
        {
            if (!TryParseUtcDateTime(request.Before, out var parsedBefore))
            {
                return ApplicationResponse<SearchMessagesResponse>.Fail(
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
                return ApplicationResponse<SearchMessagesResponse>.Fail(
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
            return ApplicationResponse<SearchMessagesResponse>.Fail(
                ApplicationErrorCodes.Common.ValidationFailed,
                "Request validation failed",
                EndpointExtensions.SingleValidationError(
                    nameof(request.After),
                    ApplicationErrorCodes.Validation.OutOfRange,
                    "After must be earlier than or equal to before"));
        }

        var guildContext = await _guildRepository.GetWithCallerRoleAsync(guildId, currentUserId, cancellationToken);
        if (guildContext is null)
        {
            _logger.LogWarning(
                "SearchMessages failed because guild was not found. GuildId={GuildId}, UserId={UserId}",
                guildId,
                currentUserId);

            return ApplicationResponse<SearchMessagesResponse>.Fail(
                ApplicationErrorCodes.Guild.NotFound,
                "Guild was not found");
        }

        if (guildContext.CallerRole is null)
        {
            _logger.LogWarning(
                "SearchMessages access denied. GuildId={GuildId}, UserId={UserId}",
                guildId,
                currentUserId);

            return ApplicationResponse<SearchMessagesResponse>.Fail(
                ApplicationErrorCodes.Guild.AccessDenied,
                "You do not have access to this guild");
        }

        if (channelId is not null)
        {
            var channelContext = await _guildChannelRepository.GetWithCallerRoleAsync(channelId, currentUserId, cancellationToken);
            if (channelContext is null || channelContext.Channel.GuildId != guildId)
            {
                _logger.LogWarning(
                    "SearchMessages failed because channel filter was not found in guild. GuildId={GuildId}, ChannelId={ChannelId}, UserId={UserId}",
                    guildId,
                    channelId,
                    currentUserId);

                return ApplicationResponse<SearchMessagesResponse>.Fail(
                    ApplicationErrorCodes.Channel.NotFound,
                    "Channel was not found");
            }

            if (channelContext.Channel.Type != GuildChannelType.Text)
            {
                _logger.LogWarning(
                    "SearchMessages failed because channel filter is not text. GuildId={GuildId}, ChannelId={ChannelId}, ChannelType={ChannelType}, UserId={UserId}",
                    guildId,
                    channelId,
                    channelContext.Channel.Type,
                    currentUserId);

                return ApplicationResponse<SearchMessagesResponse>.Fail(
                    ApplicationErrorCodes.Channel.NotText,
                    "Messages can only be searched in text channels");
            }

            if (channelContext.CallerRole is null)
            {
                _logger.LogWarning(
                    "SearchMessages access denied for channel filter. GuildId={GuildId}, ChannelId={ChannelId}, UserId={UserId}",
                    guildId,
                    channelId,
                    currentUserId);

                return ApplicationResponse<SearchMessagesResponse>.Fail(
                    ApplicationErrorCodes.Channel.AccessDenied,
                    "You do not have access to this channel");
            }
        }

        var limit = request.Limit ?? DefaultLimit;
        var page = await _channelMessageRepository.SearchGuildMessagesAsync(
            new SearchGuildMessagesQuery(
                GuildId: guildId,
                SearchText: rawQuery.Trim(),
                ChannelId: channelId,
                AuthorId: authorId,
                BeforeCreatedAtUtc: beforeCreatedAtUtc,
                AfterCreatedAtUtc: afterCreatedAtUtc,
                Cursor: cursor),
            limit,
            cancellationToken);

        _logger.LogInformation(
            "SearchMessages succeeded. GuildId={GuildId}, UserId={UserId}, ItemCount={ItemCount}, HasNextCursor={HasNextCursor}",
            guildId,
            currentUserId,
            page.Items.Count,
            page.NextCursor is not null);

        var payload = new SearchMessagesResponse(
            GuildId: guildId.ToString(),
            Items: page.Items
                .Select(item => new SearchMessagesItemResponse(
                    MessageId: item.MessageId.ToString(),
                    ChannelId: item.ChannelId.ToString(),
                    ChannelName: item.ChannelName,
                    AuthorUserId: item.AuthorUserId.ToString(),
                    AuthorUsername: item.AuthorUsername,
                    AuthorDisplayName: item.AuthorDisplayName,
                    Content: item.Content.Value,
                    CreatedAtUtc: item.CreatedAtUtc,
                    UpdatedAtUtc: item.UpdatedAtUtc))
                .ToArray(),
            NextCursor: page.NextCursor is null ? null : MessageCursorCodec.Encode(page.NextCursor));

        return ApplicationResponse<SearchMessagesResponse>.Ok(payload);
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
