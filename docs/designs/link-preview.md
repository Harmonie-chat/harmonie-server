# Link Preview — Design Document

> **Issue** : [#356](https://github.com/Harmonie-chat/harmonie-server/issues/356)

## Objectif

Lorsqu'un message contient une URL, afficher un aperçu enrichi (titre, description, image, nom du site) dans le fil de discussion, façon Discord/Slack.

## Approche retenue

**Côté serveur, résolution asynchrone avec notification SignalR.**

Le message est créé et notifié immédiatement (sans preview). Le serveur résout les previews en arrière-plan et émet un événement `MessagePreviewUpdated` dans le groupe SignalR quand c'est prêt.

### Pourquoi cette approche

| Approche | Verdict | Raison |
|---|---|---|
| Synchrone inline | ❌ | Bloque la réponse HTTP (fetch 2–5s), mauvaise UX |
| Client-side uniquement | ❌ | Expose IP client, pas de cache partagé, dépend des capabilités client |
| Service dédié | ⚠️ | Overkill pour la taille actuelle du projet |
| **Async serveur + SignalR** | ✅ | Message instantané, cache partagé, preview uniforme, sécurité serveur |

## Flux utilisateur

```
1. User envoie "Regardez ça https://example.com/article"
2. Message apparaît immédiatement dans le channel                  ← MessageCreated (existant)
3. ~1-3 secondes plus tard, la preview apparaît sous le message    ← MessagePreviewUpdated (nouveau)
4. Les utilisateurs qui rejoignent/rechargent voient le message
   avec sa preview directement (via GetMessages)                    ← lazy: inclus dans la réponse
```

## DDD — Positionnement du modèle

### L'agrégat `MessageLinkPreview` est **séparé** de `Message`

**Raisons :**

1. **Pas d'invariant commun** — un `Message` est valide avec ou sans preview. Aucune règle métier de `Message` ne dépend des previews.
2. **Consistency boundary différente** — les previews sont créées dans une transaction séparée, après la création du message.
3. **Chargement lazy naturel** — on ne veut pas charger les previews à chaque lecture de message.
4. **Aligné avec les patterns existants** — `MessageReaction` et `MessageReadState` sont déjà des entités séparées avec leurs propres repos. `MessageLinkPreview` suit le même principe.

```
Message (agrégat racine)          MessageLinkPreview (agrégat racine séparé)
├── MessageId                     ├── MessageId (référence)
├── Content                       ├── Url (clé composite avec MessageId)
├── Attachments (value objects)   ├── Title
└── ...                           ├── Description
                                  ├── ImageUrl
                                  ├── SiteName
                                  └── FetchedAtUtc
```

### Pas de cache externe en v1

La table `message_link_previews` fait office de cache naturel : avant de fetcher une URL, on vérifie si une preview récente (< 24h) existe déjà. On duplique les données dans une nouvelle ligne pour le message courant.

**→ Évolution possible vers une table `link_previews` séparée + table de liaison si le volume le justifie.**

## Structure des données

### Table SQL

```sql
-- Migration: 20260502_1_CreateMessageLinkPreviewsTable.sql
CREATE TABLE message_link_previews (
    message_id UUID NOT NULL REFERENCES messages(id) ON DELETE CASCADE,
    url TEXT NOT NULL,
    title TEXT,
    description TEXT,
    image_url TEXT,
    site_name TEXT,
    fetched_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (message_id, url)
);

CREATE INDEX idx_link_previews_url_fetched ON message_link_previews(url, fetched_at_utc DESC);
```

Note : pas de colonne `id` — l'identité est la paire `(message_id, url)`, cohérent avec `MessageReaction` qui utilise `(message_id, user_id, emoji)` comme clé composite.

### Entity Domain

```csharp
// Domain/Entities/Messages/MessageLinkPreview.cs
public sealed class MessageLinkPreview
{
    public MessageId MessageId { get; }
    public string Url { get; }
    public string? Title { get; }
    public string? Description { get; }
    public string? ImageUrl { get; }
    public string? SiteName { get; }
    public DateTime FetchedAtUtc { get; }

    // Factory: Create(messageId, url, ...) -> Result<MessageLinkPreview>
    // Factory: Rehydrate(...) -> MessageLinkPreview
}
```

### DTO (pour SignalR et réponses API)

```csharp
// Application/Common/Messages/LinkPreviewDto.cs
public sealed record LinkPreviewDto(
    string Url,
    string? Title,
    string? Description,
    string? ImageUrl,
    string? SiteName);
```

## Extensions des contrats existants

### 1. `ITextChannelNotifier` — nouvelle notification

```csharp
// Ajout dans l'interface existante
Task NotifyMessagePreviewUpdatedAsync(
    TextChannelMessagePreviewUpdatedNotification notification,
    CancellationToken cancellationToken = default);

// Nouveau type de notification
public sealed record TextChannelMessagePreviewUpdatedNotification(
    MessageId MessageId,
    GuildChannelId ChannelId,
    GuildId GuildId,
    IReadOnlyList<LinkPreviewDto> Previews);
```

### 2. `IConversationMessageNotifier` — nouvelle notification (miroir)

```csharp
Task NotifyMessagePreviewUpdatedAsync(
    ConversationMessagePreviewUpdatedNotification notification,
    CancellationToken cancellationToken = default);

public sealed record ConversationMessagePreviewUpdatedNotification(
    MessageId MessageId,
    ConversationId ConversationId,
    IReadOnlyList<LinkPreviewDto> Previews);
```

### 3. `IRealtimeClient` — nouvel événement client

```csharp
Task MessagePreviewUpdated(MessagePreviewUpdatedEvent payload, CancellationToken ct);

public sealed record MessagePreviewUpdatedEvent(
    Guid MessageId,
    Guid? ChannelId,        // null si conversation
    Guid? ConversationId,   // null si channel
    Guid? GuildId,          // null si conversation
    IReadOnlyList<LinkPreviewDto> Previews);
```

### 4. `GetMessagesItemResponse` — inclusion lazy des previews (v2 ou fin de v1)

```csharp
// Ajout du champ (optionnel car les previews peuvent ne pas encore exister)
IReadOnlyList<LinkPreviewDto>? LinkPreviews
```

→ Requiert un ajout dans `MessagePaginationRepository` pour joindre `message_link_previews` dans la même requête (3ᵉ query dans le multi-query).

## Nouveaux composants

### Application — Interfaces

| Fichier | Contenu |
|---|---|
| `Application/Interfaces/Messages/ILinkPreviewRepository.cs` | `GetByMessageIdsAsync()`, `TryGetRecentPreviewAsync()`, `AddAsync()` |
| `Application/Interfaces/Messages/ILinkPreviewFetcher.cs` | `Task<LinkPreviewMetadata?> FetchAsync(Uri url, CancellationToken ct)` |

### Application — Service d'orchestration

```
Application/Features/Messages/ResolveLinkPreviews/
    LinkPreviewResolutionService.cs
```

Ce service **n'est pas un handler IAuthenticatedHandler** — c'est un service injectable qui orchestre :
1. Parse les URLs du contenu du message avec `Uri.TryCreate`
2. Pour chaque URL → `ILinkPreviewRepository.TryGetRecentPreviewAsync(url)` (cache DB < 24h)
3. Pour les URL sans preview récente → `ILinkPreviewFetcher.FetchAsync(url)` + `ILinkPreviewRepository.AddAsync()`
4. Retourne la liste des `LinkPreviewDto` résolus

### Infrastructure

| Fichier | Contenu |
|---|---|
| `Infrastructure/Persistence/Messages/LinkPreviewRepository.cs` | Implémentation Dapper |
| `Infrastructure/Services/LinkPreviewFetcher.cs` | HttpClient + HtmlAgilityPack, extraction OpenGraph / Twitter Cards |

### Modification des SendMessage handlers

**Channel** (`SendMessageHandler.cs`) et **Conversation** (`SendMessageHandler.cs`) :

Après `NotifyMessageCreatedSafelyAsync(...)`, ajouter :

```csharp
// Parse URLs from message content — only spawn resolution if URLs are present
var urls = ParseUrls(messageResult.Value.Content?.Value);
if (urls.Count > 0)
{
    _ = ResolveLinkPreviewsSafelyAsync(messageResult.Value.Id, channelId, guildId, urls);
}
```

Deux gardes pour éviter tout travail inutile :
1. **0 URL dans le contenu** → on ne spawn même pas la tâche async
2. **URLs présentes mais 0 preview résolue** (timeout, pas de OG tags, site down) → pas de persistance, pas de notif SignalR

Où `ResolveLinkPreviewsSafelyAsync` est une méthode privée qui :
1. Appelle `_linkPreviewService.ResolveForMessageAsync(messageId, urls, ct)` avec un timeout de 10s
2. Si ≥ 1 preview résolue → notifie via `_textChannelNotifier.NotifyMessagePreviewUpdatedAsync()`
3. Si 0 preview → ne fait rien (pas de SignalR, pas de ligne en BDD)
4. Capture et loggue les erreurs (best-effort)

**Parsing d'URL** : `Uri.TryCreate` sur chaque token du message séparé par des espaces. Maximum 5 URLs. On ignore les schémas non-HTTP(S) et les URLs qui ne passent pas la validation SSRF.

## Extraction OpenGraph / Twitter Cards

### Priorité d'extraction

Pour chaque champ, on essaie dans l'ordre :

| Champ | Priorité 1 | Priorité 2 | Priorité 3 |
|---|---|---|---|
| `title` | `og:title` | `twitter:title` | `<title>` |
| `description` | `og:description` | `twitter:description` | `<meta name="description">` |
| `image_url` | `og:image` | `twitter:image` | — |
| `site_name` | `og:site_name` | — | — |

### Dépendance

[HtmlAgilityPack](https://www.nuget.org/packages/HtmlAgilityPack) — léger, utilisé uniquement pour parser les `<meta>` tags.

## Sécurité

### Protection SSRF

Avant tout fetch HTTP, l'URL est validée :

```csharp
bool IsSafeUrl(Uri uri)
{
    // Bloquer les IPs privées / loopback / link-local
    var host = uri.Host;
    if (host == "localhost" || host == "[::1]") return false;
    if (IPAddress.TryParse(host, out var ip))
        return !IPAddress.IsLoopback(ip) && ip.AddressFamily == AddressFamily.InterNetwork;
    return true;
}
```

### Timeout HTTP

- Timeout global par fetch : **5 secondes**
- Timeout global par résolution (toutes URLs) : **10 secondes** (le handler est déjà en fire-and-forget)

### Content-Type filtering

Un `HEAD` request d'abord pour vérifier que le `Content-Type` est `text/html`. Si ce n'est pas le cas, on ignore l'URL (pas de preview pour un PDF, une image directe, etc.).

### Rate limiting

- Maximum **5 URLs** parsées par message
- Respecte `robots.txt` ? → Non en v1, trop complexe. On met un User-Agent identifiable.

### User-Agent

```
Harmonie-LinkPreview/1.0 (+https://github.com/...)
```

## Plan de fichier — ce qu'on touche

### Nouveaux fichiers (13)

```
Domain/Entities/Messages/
    MessageLinkPreview.cs                              ← nouvel agrégat

Application/Interfaces/Messages/
    ILinkPreviewRepository.cs                          ← port repo
    ILinkPreviewFetcher.cs                             ← port fetcher HTTP

Application/Common/Messages/
    LinkPreviewDto.cs                                  ← DTO partagé

Application/Features/Messages/ResolveLinkPreviews/
    LinkPreviewResolutionService.cs                    ← orchestration

Infrastructure/Persistence/Messages/
    LinkPreviewRepository.cs                           ← adapter Dapper

Infrastructure/Services/
    LinkPreviewFetcher.cs                              ← adapter HTTP + HtmlAgilityPack

Infrastructure/Rows/Messages/
    MessageLinkPreviewRow.cs                           ← row Dapper

tools/Harmonie.Migrations/Scripts/
    20260502_1_CreateMessageLinkPreviewsTable.sql      ← migration
```

### Fichiers modifiés (9)

```
Application/Interfaces/Channels/ITextChannelNotifier.cs        ← +NotifyMessagePreviewUpdatedAsync + notification type
Application/Interfaces/Conversations/IConversationMessageNotifier.cs ← idem
Application/Features/Channels/SendMessage/SendMessageHandler.cs  ← +LinkPreviewResolutionService + fire-and-forget
Application/Features/Conversations/SendMessage/SendMessageHandler.cs ← idem
API/RealTime/Channels/SignalRTextChannelNotifier.cs          ← implémentation nouvelle notif
API/RealTime/Conversations/SignalRConversationMessageNotifier.cs ← idem
API/RealTime/Common/IRealtimeClient.cs                       ← +MessagePreviewUpdated event
API/Configuration/RealTimeConfiguration.cs                   ← pas de changement (les notifiers existants sont déjà enregistrés)
Infrastructure/DependencyInjection.cs                        ← +ILinkPreviewRepository, +ILinkPreviewFetcher
```

## Points ouverts / décisions à prendre

1. **Tailles d'image** : Faut-il limiter la taille du téléchargement d'image (`og:image`) ? Un `Content-Length` max de 5 MB semble raisonnable, sinon on ignore l'image mais on garde le reste.

2. **GetMessages** : Inclure les previews dans `GetMessagesItemResponse` maintenant ou en v2 ? L'absence signifie que les utilisateurs qui rejoignent/rechargent verront le message sans preview jusqu'à ce qu'ils reçoivent l'événement SignalR (mais s'ils n'étaient pas connectés, ils ne le recevront pas).
   → **Recommandation** : inclure dans le même chantier, ça évite un écran "incomplet" permanent pour les messages historiques.

3. **Suppression de message** : Le `ON DELETE CASCADE` gère automatiquement la suppression des previews liées. Rien à faire.

4. **Messages édités** : Si un message est édité pour ajouter une URL, faut-il résoudre la preview ?
   → **Non en v1**. Seulement à la création. Complexité acceptable pour une v2.
