using System.Globalization;
using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Features.Users;
using Harmonie.Application.Interfaces.Channels;
using Harmonie.Application.Interfaces.Guilds;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Features.Guilds.SearchMessages;

public sealed record SearchMessagesInput(
    GuildId GuildId,
    string? Q = null,
    string? ChannelId = null,
    string? AuthorId = null,
    string? Before = null,
    string? After = null,
    string? Cursor = null,
    int? Limit = null);

public sealed class SearchMessagesHandler : IAuthenticatedHandler<SearchMessagesInput, SearchMessagesResponse>
{
    private const int DefaultLimit = 25;

    private readonly IGuildRepository _guildRepository;
    private readonly IGuildChannelRepository _guildChannelRepository;
    private readonly IMessageSearchRepository _channelMessageRepository;

    public SearchMessagesHandler(
        IGuildRepository guildRepository,
        IGuildChannelRepository guildChannelRepository,
        IMessageSearchRepository channelMessageRepository)
    {
        _guildRepository = guildRepository;
        _guildChannelRepository = guildChannelRepository;
        _channelMessageRepository = channelMessageRepository;
    }

    public async Task<ApplicationResponse<SearchMessagesResponse>> HandleAsync(
        SearchMessagesInput input,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (input.Q is not string rawQuery || string.IsNullOrWhiteSpace(rawQuery))
        {
            return ApplicationResponse<SearchMessagesResponse>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "Request validation succeeded but search query was missing.");
        }

        MessageCursor? cursor = null;
        if (input.Cursor is not null)
        {
            if (!MessageCursorCodec.TryParse(input.Cursor, out var parsedCursor) || parsedCursor is null)
            {
                return ApplicationResponse<SearchMessagesResponse>.Fail(
                    ApplicationErrorCodes.Common.ValidationFailed,
                    "Request validation failed",
                    EndpointExtensions.SingleValidationError(
                        nameof(input.Cursor),
                        ApplicationErrorCodes.Validation.InvalidFormat,
                        "Cursor is invalid"));
            }

            cursor = parsedCursor;
        }

        GuildChannelId? channelId = null;
        if (input.ChannelId is not null)
        {
            if (!GuildChannelId.TryParse(input.ChannelId, out var parsedChannelId) || parsedChannelId is null)
            {
                return ApplicationResponse<SearchMessagesResponse>.Fail(
                    ApplicationErrorCodes.Common.ValidationFailed,
                    "Request validation failed",
                    EndpointExtensions.SingleValidationError(
                        nameof(input.ChannelId),
                        ApplicationErrorCodes.Validation.InvalidFormat,
                        "Channel ID is invalid"));
            }

            channelId = parsedChannelId;
        }

        UserId? authorId = null;
        if (input.AuthorId is not null)
        {
            if (!UserId.TryParse(input.AuthorId, out var parsedAuthorId) || parsedAuthorId is null)
            {
                return ApplicationResponse<SearchMessagesResponse>.Fail(
                    ApplicationErrorCodes.Common.ValidationFailed,
                    "Request validation failed",
                    EndpointExtensions.SingleValidationError(
                        nameof(input.AuthorId),
                        ApplicationErrorCodes.Validation.InvalidFormat,
                        "Author ID is invalid"));
            }

            authorId = parsedAuthorId;
        }

        DateTime? beforeCreatedAtUtc = null;
        if (input.Before is not null)
        {
            if (!TryParseUtcDateTime(input.Before, out var parsedBefore))
            {
                return ApplicationResponse<SearchMessagesResponse>.Fail(
                    ApplicationErrorCodes.Common.ValidationFailed,
                    "Request validation failed",
                    EndpointExtensions.SingleValidationError(
                        nameof(input.Before),
                        ApplicationErrorCodes.Validation.InvalidFormat,
                        "Before must be a valid ISO 8601 date/time"));
            }

            beforeCreatedAtUtc = parsedBefore;
        }

        DateTime? afterCreatedAtUtc = null;
        if (input.After is not null)
        {
            if (!TryParseUtcDateTime(input.After, out var parsedAfter))
            {
                return ApplicationResponse<SearchMessagesResponse>.Fail(
                    ApplicationErrorCodes.Common.ValidationFailed,
                    "Request validation failed",
                    EndpointExtensions.SingleValidationError(
                        nameof(input.After),
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
                    nameof(input.After),
                    ApplicationErrorCodes.Validation.OutOfRange,
                    "After must be earlier than or equal to before"));
        }

        var guildContext = await _guildRepository.GetWithCallerRoleAsync(input.GuildId, currentUserId, cancellationToken);
        if (guildContext is null)
        {
            return ApplicationResponse<SearchMessagesResponse>.Fail(
                ApplicationErrorCodes.Guild.NotFound,
                "Guild was not found");
        }

        if (guildContext.CallerRole is null)
        {
            return ApplicationResponse<SearchMessagesResponse>.Fail(
                ApplicationErrorCodes.Guild.AccessDenied,
                "You do not have access to this guild");
        }

        if (channelId is not null)
        {
            var channelContext = await _guildChannelRepository.GetWithCallerRoleAsync(channelId, currentUserId, cancellationToken);
            if (channelContext is null || channelContext.Channel.GuildId != input.GuildId)
            {
                return ApplicationResponse<SearchMessagesResponse>.Fail(
                    ApplicationErrorCodes.Channel.NotFound,
                    "Channel was not found");
            }

            if (channelContext.Channel.Type != GuildChannelType.Text)
            {
                return ApplicationResponse<SearchMessagesResponse>.Fail(
                    ApplicationErrorCodes.Channel.NotText,
                    "Messages can only be searched in text channels");
            }

            if (channelContext.CallerRole is null)
            {
                return ApplicationResponse<SearchMessagesResponse>.Fail(
                    ApplicationErrorCodes.Channel.AccessDenied,
                    "You do not have access to this channel");
            }
        }

        var limit = input.Limit ?? DefaultLimit;
        var page = await _channelMessageRepository.SearchGuildMessagesAsync(
            new SearchGuildMessagesQuery(
                GuildId: input.GuildId,
                SearchText: rawQuery.Trim(),
                ChannelId: channelId,
                AuthorId: authorId,
                BeforeCreatedAtUtc: beforeCreatedAtUtc,
                AfterCreatedAtUtc: afterCreatedAtUtc,
                Cursor: cursor),
            limit,
            cancellationToken);

        var payload = new SearchMessagesResponse(
            GuildId: input.GuildId.ToString(),
            Items: page.Items
                .Select(item =>
                {
                    var authorAvatar = item.AuthorAvatarColor is not null || item.AuthorAvatarIcon is not null || item.AuthorAvatarBg is not null
                        ? new AvatarAppearanceDto(item.AuthorAvatarColor, item.AuthorAvatarIcon, item.AuthorAvatarBg)
                        : null;

                    return new SearchMessagesItemResponse(
                        MessageId: item.MessageId.ToString(),
                        ChannelId: item.ChannelId.ToString(),
                        ChannelName: item.ChannelName,
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
