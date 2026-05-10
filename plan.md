# ChatHub — Implementation Plan (Native WebSockets, NATS Core, MongoDB)

## Architecture decisions

- **Transport**: ASP.NET Core native `WebSocketMiddleware` — no SignalR.
- **Backplane**: NATS core pub/sub (no JetStream) — plain publish/subscribe with queue groups for load-balanced pod delivery.
- **Persistence**: MongoDB via the official .NET driver — no EF Core, no migrations.
- **Blob storage**: S3-compatible (MinIO for local, AWS S3 for production) for voice and file blobs.
- **Cache**: MongoDB for distributed presence, session state, and rate limiting; in-memory for voice chunk assembly.

All clients (web, iOS, Android) connect using their platform's native WebSocket client. There is no SignalR client SDK dependency on any platform.

> **NATS delivery guarantee**: NATS core is at-most-once. There is no message replay or redelivery. Durability is owned entirely by the MongoDB write path — every message is persisted to MongoDB before being published to NATS for fan-out. Clients can re-fetch missed messages from MongoDB on reconnect.

---

## Phase 1 — Core WebSocket server

### 1.1 WebSocket middleware and connection acceptor

Create `WebSocketMiddleware` registered at `/ws`:

- Call `HttpContext.WebSockets.AcceptWebSocketAsync()` after validating the JWT from the `Authorization` query parameter or `Sec-WebSocket-Protocol` header (HTTP upgrade does not support the Authorization header natively).
- On accept, instantiate a `WebSocketConnection` record holding: connection ID (GUID), authenticated `ClaimsPrincipal`, the raw `WebSocket` instance, and a `CancellationTokenSource` tied to the connection lifetime.
- Register the connection in `IConnectionRegistry` (in-memory `ConcurrentDictionary`, keyed by connection ID).
- Start two concurrent tasks: `ReceiveLoopAsync` and `HeartbeatLoopAsync`. Await both with `Task.WhenAll`.
- On loop exit (client disconnect or error), deregister from registry, publish a presence-leave event to NATS, and dispose the WebSocket.

### 1.2 Receive loop

`ReceiveLoopAsync` reads frames in a loop using `WebSocket.ReceiveAsync(buffer, ct)`:

- Use a 4KB stack-allocated buffer with `ArrayPool<byte>` for larger messages; reassemble fragmented frames into a `MemoryStream` until `EndOfMessage` is true.
- On `WebSocketMessageType.Text`: deserialize the buffer as a `ClientMessage` envelope (System.Text.Json, camelCase).
- On `WebSocketMessageType.Binary`: treat as a raw voice chunk; extract the connection ID and conversation ID from the first 36 + 36 bytes (two GUIDs as ASCII), then treat the remainder as the audio payload.
- On `WebSocketMessageType.Close`: send `WebSocket.CloseAsync` and break the loop.
- Wrap the entire loop body in try/catch; log structured errors; never let an exception crash the loop.

### 1.3 Heartbeat loop

`HeartbeatLoopAsync` runs every 15 seconds:

- Send a `WebSocketMessageType.Text` ping frame: `{"type":"ping"}`.
- Expect a `{"type":"pong"}` within 10 seconds; track last-pong timestamp on the connection record.
- If no pong received within timeout, abort the connection (cancel the CTS).
- Idle connections with no traffic for 30 minutes are also aborted.

### 1.4 Send abstraction

Create `IWebSocketSender` with:

```csharp
Task SendTextAsync(string connectionId, ReadOnlyMemory<byte> utf8Json, CancellationToken ct);
Task SendBinaryAsync(string connectionId, ReadOnlyMemory<byte> data, CancellationToken ct);
Task BroadcastToServiceAsync(string serviceId, ReadOnlyMemory<byte> utf8Json, CancellationToken ct);
```

Sending must be serialized per connection — WebSocket does not allow concurrent sends. Use a `Channel<SendItem>` per connection as a send queue; a dedicated send loop drains the channel sequentially.

---

## Phase 2 — Wire protocol (JSON envelope)

All text frames carry a typed JSON envelope defined by a `type` discriminator field.

### Client → server message types

| Type | Fields |
|---|---|
| `join_service` | `serviceId` |
| `leave_service` | `serviceId` |
| `text_message` | `id`, `conversationId`, `serviceId`, `text`, `replyToId?` |
| `voice_chunk` | `id`, `conversationId`, `sequenceNumber`, `isFinal` + binary frame immediately follows |
| `voice_message` | `id`, `conversationId`, `blobId`, `durationMs`, `mimeType` |
| `file_attachment` | `id`, `conversationId`, `blobId`, `fileName`, `mimeType`, `sizeBytes` |
| `typing` | `conversationId`, `isTyping` |
| `ack` | `messageId` |
| `pong` | — |

### Server → client message types

| Type | Fields |
|---|---|
| `message_received` | `envelope: MessageEnvelope` |
| `voice_chunk` | `id`, `conversationId`, `sequenceNumber`, `isFinal`, `fromUserId` |
| `user_joined` | `userId`, `serviceId`, `displayName` |
| `user_left` | `userId`, `serviceId` |
| `typing` | `userId`, `conversationId`, `isTyping` |
| `delivered` | `messageId` |
| `error` | `code`, `message`, `correlationId` |
| `ping` | — |

Implement `IMessageDispatcher` that receives a deserialized `ClientMessage`, pattern-matches on `type`, and routes to the appropriate handler service. Each handler is a scoped `IMessageHandler<T>` registered in DI.

---

## Phase 3 — Service and room routing

### 3.1 Connection registry

`IConnectionRegistry` tracks:

- All active `WebSocketConnection` instances by connection ID.
- A per-service index: `ConcurrentDictionary<string, ConcurrentHashSet<string>>` mapping `serviceId` → set of connection IDs.
- A per-user index: `ConcurrentDictionary<string, ConcurrentHashSet<string>>` mapping `userId` → set of connection IDs (one user may have multiple connections).

### 3.2 Join / leave handling

On `join_service`:

- Add connection ID to the service index.
- Publish `chathub.{serviceId}.presence` on NATS: `{ event: "joined", userId, connectionId, podId }`.
- Send `user_joined` to all connections in that service on this pod.
- NATS fan-out delivers to other pods.

On `leave_service` or disconnect:

- Remove from service index.
- Publish presence-leave to NATS.

---

## Phase 4 — NATS core backplane

NATS core pub/sub is used exclusively for real-time cross-pod fan-out. It is **not** used for durability — MongoDB owns persistence. No JetStream, no streams, no consumers, no ack semantics.

### 4.1 Subject conventions

| Subject | Purpose |
|---|---|
| `chathub.{serviceId}.messages` | Text, voice envelope, file attachment fan-out |
| `chathub.{serviceId}.presence` | Join, leave, typing events |
| `chathub.system.broadcast` | Platform-wide messages to all pods |

### 4.2 Publisher

`INatsBackplane.PublishAsync(string subject, ReadOnlyMemory<byte> payload)`:

- Always publish **after** the MongoDB write succeeds — never before. MongoDB is the source of truth; NATS is the delivery bus.
- Include a `source-pod` header (pod hostname) so receiving pods skip re-delivering to connections that already received the message via local dispatch.
- Fire-and-forget. If a subscriber pod is down the NATS message is lost — the client fetches missed history from MongoDB on reconnect.

### 4.3 Subscriber (`IHostedService`)

`NatsSubscriberService : BackgroundService`:

- On startup, subscribe to `chathub.*.messages` and `chathub.*.presence` using a **queue group** named `chathub-hub`. Queue groups ensure each NATS message is delivered to exactly one pod in the group, preventing duplicate fan-out across replicas.
- Subscribe to `chathub.system.broadcast` **without** a queue group — intentional broadcast to all pods.
- For each received message, parse the subject to extract `serviceId`, look up all local connections in `IConnectionRegistry`, and push the payload to their send queues.

### 4.4 NATS cluster configuration for Kubernetes

NATS runs as a 3-node cluster, no JetStream, fully in-memory. No PVCs required.

```yaml
cluster:
  enabled: true
  replicas: 3
nats:
  jetstream:
    enabled: false
```

---

## Phase 5 — Voice handling

### 5.1 Live voice streaming

Voice chunks arrive as a text envelope (`voice_chunk` type) immediately followed by a binary frame containing the raw audio bytes. The receive loop correlates them by sequence within the connection context.

Server-side, chunks are:

1. Forwarded immediately to all other connections in the conversation via their send queues (low-latency, bypasses NATS for same-pod connections; NATS for cross-pod).
2. Accumulated in a `VoiceSessionBuffer` stored in pod-local memory with sequence ordering keyed by `voice:{messageId}`.

On `isFinal: true`:

- Retrieve all chunks from pod-local memory in sequence order.
- Assemble into a single buffer.
- Upload to blob storage (S3) via `IBlobStorageClient`.
- Persist a `voice_message` document to MongoDB via `IMessageRepository`.
- Publish the `voice_message` envelope to NATS for cross-pod fan-out.
- Remove the in-memory session immediately.

### 5.2 Pre-recorded voice

Clients upload the audio file to `POST /api/upload/voice` (multipart, max 25 MB) before sending the hub message. The endpoint returns a `blobId`. The client then sends a `voice_message` envelope over WebSocket with the `blobId`.

---

## Phase 6 — File attachments

`POST /api/upload/file` (multipart, max 100 MB):

- Validate JWT from `Authorization` header.
- Stream directly to blob storage — do not buffer in memory.
- Return `{ blobId, fileName, mimeType, sizeBytes, url }`.

The client sends a `file_attachment` WebSocket message with the returned metadata. The server persists the document to MongoDB and publishes to NATS for fan-out.

---

## Phase 7 — Auth

### 7.1 WebSocket handshake auth

HTTP upgrade requests cannot carry an `Authorization` header reliably across all platforms. Accept the JWT via:

- Query parameter: `/ws?token=<jwt>` (validated server-side; stripped from logs).
- Or `Sec-WebSocket-Protocol` header with a custom subprotocol value encoding the token (less common).

Validate using `JwtBearerHandler` logic extracted into a standalone `IJwtValidator` service. If invalid, return HTTP 401 before calling `AcceptWebSocketAsync`.

### 7.2 Per-message auth

Every handler checks the `ClaimsPrincipal` on the `WebSocketConnection` — no re-validation per message since the token is validated once on connect. Token expiry is enforced by closing the connection when the JWT `exp` claim elapses, checked in the heartbeat loop.

---

## Phase 8 — Rate limiting

Implement `IRateLimiter` backed by MongoDB sliding window counters:

- 100 text/file messages per connection per minute.
- 10 voice messages per connection per minute.

Checked in each message handler before processing. On limit exceeded, send an `error` envelope with code `rate_limit_exceeded` and drop the message. Do not close the connection.

---

## Phase 9 — Persistence (MongoDB)

Use the official MongoDB .NET driver (`MongoDB.Driver`). No EF Core, no migrations. Collections and indexes are created on application startup via `MongoInitializer : IHostedService`.

### Collections

#### `messages`

```json
{
  "_id": "ObjectId",
  "conversationId": "string",
  "serviceId": "string",
  "senderId": "string",
  "type": "text | voice | file",
  "text": "string | null",
  "voice": { "blobId": "string", "durationMs": 0, "mimeType": "string" },
  "file": { "blobId": "string", "fileName": "string", "mimeType": "string", "sizeBytes": 0 },
  "replyToId": "string | null",
  "createdAt": "ISODate",
  "deliveredAt": "ISODate | null"
}
```

Indexes: `{ conversationId: 1, createdAt: -1 }` (primary query pattern), `{ serviceId: 1, createdAt: -1 }`.

#### `conversations`

```json
{
  "_id": "ObjectId",
  "serviceId": "string",
  "participantIds": ["string"],
  "createdAt": "ISODate",
  "lastMessageAt": "ISODate"
}
```

Indexes: `{ serviceId: 1 }`, `{ participantIds: 1 }`.

#### `connections` (ephemeral audit log)

```json
{
  "_id": "ObjectId",
  "userId": "string",
  "connectionId": "string",
  "serviceId": "string",
  "podId": "string",
  "connectedAt": "ISODate",
  "disconnectedAt": "ISODate | null"
}
```

TTL index on `disconnectedAt` with 24-hour expiry to auto-prune stale records.

### Write strategy

Write messages asynchronously via a `Channel<MessageDocument>` drained by `MongoWriterService : BackgroundService`. Do not await the MongoDB write in the hot receive path. The NATS publish happens inside the background writer, after `InsertOneAsync` completes successfully.

### Repository pattern

Define `IMessageRepository` and `IConversationRepository` in `ChatHub.Core`. Implement with `IMongoCollection<T>` directly in `ChatHub.Infrastructure/Persistence` — no ODM abstraction needed.

---

## Phase 10 — Kubernetes manifests

Generate the following complete YAML manifests.

### `deployment.yaml`

- Image: `chathub-api:latest`
- Replicas: 3 (HPA manages 3–20)
- Resources: request 256Mi / 0.25 CPU, limit 1Gi / 1 CPU
- Env vars sourced from ConfigMap and Secrets
- Liveness: `GET /health` (30s initial delay, 10s period)
- Readiness: `GET /health/ready` (checks NATS + MongoDB)
- Lifecycle `preStop`: sleep 5s to allow graceful WebSocket drain

### `service.yaml`

ClusterIP on port 80 → container 8080.

### `ingress.yaml` (nginx)

Required annotations:

```yaml
nginx.ingress.kubernetes.io/proxy-read-timeout: "3600"
nginx.ingress.kubernetes.io/proxy-send-timeout: "3600"
nginx.ingress.kubernetes.io/proxy-http-version: "1.1"
nginx.ingress.kubernetes.io/proxy-set-header: "Upgrade $http_upgrade"
nginx.ingress.kubernetes.io/proxy-set-header: "Connection Upgrade"
```

TLS termination at ingress; backend plain HTTP.

### `hpa.yaml`

Scale on CPU 70% + custom metric `active_websocket_connections` > 8000 per pod.

### `pdb.yaml`

`minAvailable: 2`

### `configmap.yaml`

`MaxMessageSizeBytes`, `AllowedOrigins`, `PingIntervalSeconds`, `IdleTimeoutMinutes`, `RateLimitTextPerMinute`, `RateLimitVoicePerMinute`, `MongoDatabase`.

### `secret.yaml` (template with placeholder values)

`JwtSigningKey`, `MongoConnectionString`, `S3AccessKey`, `S3SecretKey`.

> No `NatsCredentials` secret is needed for unauthenticated in-cluster NATS. Add if mTLS or token auth is enabled on the NATS cluster.

### `nats-values.yaml`

```yaml
cluster:
  enabled: true
  replicas: 3
nats:
  jetstream:
    enabled: false
```

No PVCs required. NATS runs fully in memory.

---

## Phase 11 — Local development

`docker-compose.yml` services:

| Service | Image | Ports | Notes |
|---|---|---|---|
| `chathub-api` | build from Dockerfile | 8080:8080 | hot-reload via `dotnet watch` |
| `nats` | `nats:latest` | 4222 | no `-js` flag needed |
| `mongo` | `mongo:7` | 27017 | volume for data |

| `minio` | `minio/minio:latest` | 9000, 9001 | S3-compatible blob storage |

---

## Phase 12 — Solution structure

```
ChatHub.sln
├── ChatHub.Api/
│   ├── Program.cs
│   ├── Middleware/
│   │   └── WebSocketMiddleware.cs
│   ├── Handlers/                      ← one IMessageHandler<T> per client message type
│   ├── Controllers/
│   │   └── UploadController.cs
│   └── HealthChecks/
    │       ├── NatsHealthCheck.cs
    │       └── MongoHealthCheck.cs
├── ChatHub.Core/
│   ├── Models/                        ← ClientMessage, ServerMessage, MessageEnvelope DTOs
│   ├── Documents/                     ← MessageDocument, ConversationDocument (MongoDB shapes)
│   ├── Interfaces/
│   │   ├── IConnectionRegistry.cs
│   │   ├── IWebSocketSender.cs
│   │   ├── INatsBackplane.cs
│   │   ├── IMessageDispatcher.cs
│   │   ├── IMessageRepository.cs
│   │   ├── IConversationRepository.cs
│   │   ├── IBlobStorageClient.cs
│   │   └── IRateLimiter.cs
│   └── Settings/
│       ├── ChatHubSettings.cs
│       ├── NatsSettings.cs
│       ├── MongoSettings.cs
│       └── StorageSettings.cs
├── ChatHub.Infrastructure/
│   ├── WebSockets/
│   │   ├── WebSocketConnection.cs
│   │   ├── ConnectionRegistry.cs
│   │   └── WebSocketSender.cs
│   ├── Nats/
│   │   ├── NatsBackplane.cs
│   │   └── NatsSubscriberService.cs   ← BackgroundService, queue group subscriber
│   ├── Persistence/
│   │   ├── MongoInitializer.cs        ← IHostedService, creates indexes on startup
│   │   ├── MessageRepository.cs
│   │   └── ConversationRepository.cs
│   ├── Writers/
│   │   └── MongoWriterService.cs      ← BackgroundService, drains Channel<MessageDocument>
    │   ├── Cache/
    │   │   ├── InMemoryPresenceService.cs
    │   │   └── InMemoryRateLimiter.cs
│   └── Storage/
│       └── S3BlobStorageClient.cs
└── ChatHub.Tests/
    ├── Unit/
    │   ├── Handlers/
    │   └── WebSockets/
    └── Integration/
        ├── WebSocketTests.cs          ← WebApplicationFactory + ClientWebSocket
        ├── NatsTests.cs              ← Testcontainers.Nats
        └── MongoTests.cs             ← Testcontainers.MongoDb
```

---

## Deliverable order for SpecKit

Generate in this sequence — each phase depends on the previous:

1. Core models, DTOs, and MongoDB document shapes (`ChatHub.Core`)
2. All interfaces in `ChatHub.Core/Interfaces`
3. All settings classes in `ChatHub.Core/Settings`
4. `WebSocketConnection`, `ConnectionRegistry`, `WebSocketSender`
5. `WebSocketMiddleware` with receive loop and heartbeat loop
6. `IMessageDispatcher` and all `IMessageHandler<T>` implementations
7. `NatsBackplane` and `NatsSubscriberService` (queue group)
8. `MongoInitializer` (index setup on startup)
9. `MessageRepository` and `ConversationRepository`
10. `MongoWriterService` (background channel drain + NATS publish)
11. `MongoDbPresenceService` and `MongoDbRateLimiter`
12. `S3BlobStorageClient`
13. `UploadController` (voice + file)
14. Health check implementations (NATS, MongoDB)
15. `Program.cs` — full DI wiring and middleware pipeline
16. Kubernetes YAML manifests (all 7 files)
17. `nats-values.yaml` Helm override
18. `docker-compose.yml`
19. Unit and integration test stubs
20. README with wire protocol reference and client connection examples (JS, Swift, Kotlin)