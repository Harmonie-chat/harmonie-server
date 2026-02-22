# Guild Text API Contract

## Conventions

- Authentication: JWT bearer required on all endpoints below.
- Success payload: feature DTO directly.
- Error payload: `ApplicationError`.

`ApplicationError` shape:

```json
{
  "code": "GUILD_NOT_FOUND",
  "message": "Guild was not found",
  "details": null
}
```

## Endpoints

## Create Guild

- Method: `POST`
- Route: `/api/guilds`

Request:

```json
{
  "name": "Harmonie Team"
}
```

Success `201 Created`:

```json
{
  "guildId": "8c9f2bf6-c203-4f2e-8af8-5224f48f73e9",
  "name": "Harmonie Team",
  "ownerUserId": "d3214f0d-4dd8-4f33-a95f-57f6154ee98e",
  "defaultTextChannelId": "06386ca8-d830-4bde-a8eb-a43384d1a626",
  "defaultVoiceChannelId": "189b7c29-9b63-43c0-acd9-05c3fc90b15a",
  "createdAtUtc": "2026-02-22T10:00:00Z"
}
```

Errors:

- `400`: `COMMON_VALIDATION_FAILED`
- `409`: `GUILD_NAME_CONFLICT` (optional, if uniqueness policy added later)

## Invite Member

- Method: `POST`
- Route: `/api/guilds/{guildId}/members/invite`

Request:

```json
{
  "userId": "18a2da26-6171-4ab5-b5c0-cf28f4f3a7b8"
}
```

Success `200 OK`:

```json
{
  "guildId": "8c9f2bf6-c203-4f2e-8af8-5224f48f73e9",
  "userId": "18a2da26-6171-4ab5-b5c0-cf28f4f3a7b8",
  "role": "Member",
  "joinedAtUtc": "2026-02-22T10:03:00Z"
}
```

Errors:

- `403`: `GUILD_INVITE_FORBIDDEN`
- `404`: `GUILD_NOT_FOUND`
- `404`: `GUILD_INVITE_TARGET_NOT_FOUND`
- `409`: `GUILD_MEMBER_ALREADY_EXISTS`

## List Guild Channels

- Method: `GET`
- Route: `/api/guilds/{guildId}/channels`

Success `200 OK`:

```json
{
  "guildId": "8c9f2bf6-c203-4f2e-8af8-5224f48f73e9",
  "channels": [
    {
      "channelId": "06386ca8-d830-4bde-a8eb-a43384d1a626",
      "name": "general",
      "type": "Text",
      "isDefault": true,
      "position": 0
    },
    {
      "channelId": "189b7c29-9b63-43c0-acd9-05c3fc90b15a",
      "name": "General Voice",
      "type": "Voice",
      "isDefault": true,
      "position": 1
    }
  ]
}
```

Errors:

- `403`: `GUILD_ACCESS_DENIED`
- `404`: `GUILD_NOT_FOUND`

## Send Message

- Method: `POST`
- Route: `/api/channels/{channelId}/messages`

Request:

```json
{
  "content": "Hello team"
}
```

Success `201 Created`:

```json
{
  "messageId": "f5d8fbc2-af30-49fb-9a07-b9b7a3ebb8f6",
  "channelId": "06386ca8-d830-4bde-a8eb-a43384d1a626",
  "authorUserId": "d3214f0d-4dd8-4f33-a95f-57f6154ee98e",
  "content": "Hello team",
  "createdAtUtc": "2026-02-22T10:05:00Z"
}
```

Errors:

- `400`: `COMMON_VALIDATION_FAILED`
- `403`: `CHANNEL_ACCESS_DENIED`
- `404`: `CHANNEL_NOT_FOUND`
- `409`: `CHANNEL_NOT_TEXT`

## Get Channel Messages

- Method: `GET`
- Route: `/api/channels/{channelId}/messages?before={cursor}&limit={n}`

Query rules:

- `limit` default `50`
- `limit` min `1`, max `100`
- `before` optional cursor based on `(createdAtUtc, messageId)`

Success `200 OK`:

```json
{
  "channelId": "06386ca8-d830-4bde-a8eb-a43384d1a626",
  "items": [
    {
      "messageId": "f5d8fbc2-af30-49fb-9a07-b9b7a3ebb8f6",
      "authorUserId": "d3214f0d-4dd8-4f33-a95f-57f6154ee98e",
      "content": "Hello team",
      "createdAtUtc": "2026-02-22T10:05:00Z"
    }
  ],
  "nextCursor": null
}
```

Errors:

- `403`: `CHANNEL_ACCESS_DENIED`
- `404`: `CHANNEL_NOT_FOUND`
- `409`: `CHANNEL_NOT_TEXT`

## Error Code Catalog (Guild and Text)

- `GUILD_NOT_FOUND`
- `GUILD_ACCESS_DENIED`
- `GUILD_INVITE_FORBIDDEN`
- `GUILD_INVITE_TARGET_NOT_FOUND`
- `GUILD_MEMBER_ALREADY_EXISTS`
- `CHANNEL_NOT_FOUND`
- `CHANNEL_NOT_TEXT`
- `CHANNEL_ACCESS_DENIED`
- `MESSAGE_CONTENT_EMPTY`
- `MESSAGE_CONTENT_TOO_LONG`
