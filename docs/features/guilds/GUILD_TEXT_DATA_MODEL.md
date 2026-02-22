# Guild Text Data Model

## Domain Objects

## Guild

- `Id: GuildId`
- `Name: GuildName`
- `OwnerUserId: UserId`
- `CreatedAtUtc: DateTime`
- `UpdatedAtUtc: DateTime`

Invariants:

- name must be valid (`3..100`, trimmed).
- owner must exist.

## GuildMember

- `GuildId: GuildId`
- `UserId: UserId`
- `Role: GuildRole` (`Admin`, `Member`)
- `JoinedAtUtc: DateTime`
- `InvitedByUserId: UserId?`

Invariants:

- one membership max per `(GuildId, UserId)`.
- creator membership is `Admin`.

## GuildChannel

- `Id: ChannelId`
- `GuildId: GuildId`
- `Name: string`
- `Type: ChannelType` (`Text`, `Voice`)
- `IsDefault: bool`
- `Position: int`
- `CreatedAtUtc: DateTime`

Invariants:

- channel name required.
- only one default text channel per guild.
- only one default voice channel per guild in this phase.

## ChannelMessage

- `Id: MessageId`
- `ChannelId: ChannelId`
- `AuthorUserId: UserId`
- `Content: string`
- `CreatedAtUtc: DateTime`

Invariants:

- content required and trimmed.
- content length `1..4000`.
- author must be member of channel guild.

## PostgreSQL Schema Blueprint

## `guilds`

- `id UUID PRIMARY KEY`
- `name VARCHAR(100) NOT NULL`
- `owner_user_id UUID NOT NULL REFERENCES users(id)`
- `created_at_utc TIMESTAMPTZ NOT NULL`
- `updated_at_utc TIMESTAMPTZ NOT NULL`

Indexes:

- `idx_guilds_owner_user_id (owner_user_id)`

## `guild_members`

- `guild_id UUID NOT NULL REFERENCES guilds(id) ON DELETE CASCADE`
- `user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE`
- `role SMALLINT NOT NULL`
- `joined_at_utc TIMESTAMPTZ NOT NULL`
- `invited_by_user_id UUID NULL REFERENCES users(id)`
- `PRIMARY KEY (guild_id, user_id)`

Indexes:

- `idx_guild_members_user_id (user_id)`
- `idx_guild_members_guild_id_role (guild_id, role)`

## `guild_channels`

- `id UUID PRIMARY KEY`
- `guild_id UUID NOT NULL REFERENCES guilds(id) ON DELETE CASCADE`
- `name VARCHAR(100) NOT NULL`
- `type SMALLINT NOT NULL`
- `is_default BOOLEAN NOT NULL`
- `position INT NOT NULL`
- `created_at_utc TIMESTAMPTZ NOT NULL`

Indexes and constraints:

- `idx_guild_channels_guild_id (guild_id)`
- `idx_guild_channels_guild_id_type_position (guild_id, type, position)`
- unique partial index for default text channel per guild:
  - `UNIQUE (guild_id) WHERE type = 1 AND is_default = true`
- unique partial index for default voice channel per guild:
  - `UNIQUE (guild_id) WHERE type = 2 AND is_default = true`

## `channel_messages`

- `id UUID PRIMARY KEY`
- `channel_id UUID NOT NULL REFERENCES guild_channels(id) ON DELETE CASCADE`
- `author_user_id UUID NOT NULL REFERENCES users(id)`
- `content VARCHAR(4000) NOT NULL`
- `created_at_utc TIMESTAMPTZ NOT NULL`

Indexes:

- `idx_channel_messages_channel_created (channel_id, created_at_utc DESC, id DESC)`

## Migration Strategy

Planned migration scripts:

1. `20260222_01_CreateGuilds.sql`
2. `20260222_02_CreateGuildMembers.sql`
3. `20260222_03_CreateGuildChannels.sql`
4. `20260222_04_CreateChannelMessages.sql`

Notes:

- keep scripts small and reversible in intent.
- create all required indexes in migration files, not ad hoc.
- use explicit default channel inserts in application transaction during guild creation.

## Transaction Boundaries

Guild creation must be atomic:

1. insert guild
2. insert owner membership (`Admin`)
3. insert default text channel
4. insert default voice channel placeholder

If one step fails, rollback entire transaction.
