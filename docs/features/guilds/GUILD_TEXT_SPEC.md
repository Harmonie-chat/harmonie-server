# Guild Text Feature Specification

## Scope

This document defines the first guild release focused on text collaboration.

In scope:

- create guild
- automatic owner admin role assignment
- invite an existing user as member
- create default channels on guild creation:
  - one default text channel
  - one default voice channel placeholder
- list guild channels
- send and read text messages in guild text channels

Out of scope for this cycle:

- voice communication behavior
- moderation features (mute/ban/kick/message deletion)
- thread support
- reactions
- file attachments
- typing indicators
- read receipts

## Vocabulary

- Guild: tenant container for members and channels.
- Member: user linked to guild with a role.
- Admin: member allowed to invite new members.
- Text Channel: channel that stores text messages.
- Voice Channel: channel placeholder for future real-time voice features.

## Roles and Permissions

- `Admin`
  - create guild (as creator)
  - invite members
  - read and send messages in text channels
- `Member`
  - read and send messages in text channels
  - cannot invite members in phase 1

## Functional Requirements

- FR-01: Authenticated users can create a guild with a valid name.
- FR-02: Guild creator becomes member with role `Admin`.
- FR-03: Guild creation must automatically create:
  - default text channel `general`
  - default voice channel `General Voice`
- FR-04: Admin can invite an existing user to guild.
- FR-05: Invited user becomes `Member` immediately.
- FR-06: Duplicate membership must be rejected.
- FR-07: Only guild members can list guild channels.
- FR-08: Only guild members can read messages in guild text channels.
- FR-09: Only guild members can send messages in guild text channels.
- FR-10: Messages are returned sorted by creation time ascending within a page.
- FR-11: Message pagination must be supported.

## Validation Rules

- Guild name:
  - required
  - length 3..100
  - trimmed
- Channel message content:
  - required
  - length 1..4000
  - trimmed before persistence
- Invite target:
  - target user must exist
  - inviter must be guild admin
  - target must not already be member

## Security Rules

- All guild and channel endpoints require valid JWT authentication.
- Authorization checks must happen at application boundary and persistence filters.
- Do not leak guild existence to unauthorized users where possible.

## Error Handling Convention

Public API errors use `ApplicationError`:

```json
{
  "code": "GUILD_NOT_FOUND",
  "message": "Guild was not found",
  "details": null
}
```

Internal application flow uses `ApplicationResponse<T>` and never relies on business exceptions for expected flows.

## Acceptance Scenario (Primary)

1. Register/Login user A.
2. Register/Login user B.
3. User A creates guild.
4. User A invites user B.
5. User B lists guild channels and sees default text channel.
6. User A posts message in default text channel.
7. User B fetches messages and sees user A message.
