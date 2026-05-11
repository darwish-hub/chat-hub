# ChatHub - Real-Time Chat Service

A high-performance real-time chat service built with .NET 8, featuring native WebSocket support, live voice messaging, file sharing, presence tracking, delivery acknowledgments, and message replies.

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                        Clients                               │
│  (WebSocket / REST API)                                     │
└──────────────────────┬──────────────────────────────────────┘
                       │ HTTPS/WSS (JWT Auth)
┌──────────────────────▼──────────────────────────────────────┐
│                    Kubernetes Cluster                        │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │   Pod 1      │  │   Pod 2      │  │   Pod N      │      │
│  │ ┌──────────┐ │  │ ┌──────────┐ │  │ ┌──────────┐ │      │
│  │ │ChatHub   │ │  │ │ChatHub   │ │  │ │ChatHub   │ │      │
│  │ │API       │ │  │ │API       │ │  │ │API       │ │      │
│  │ └──────────┘ │  │ └──────────┘ │  │ └──────────┘ │      │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘      │
└─────────┼─────────────────┼─────────────────┼──────────────┘
          │                 │                 │
          └─────────────────┼─────────────────┘
                            │
┌───────────────────────────▼────────────────────────────────┐
│              Infrastructure Layer                           │
│  ┌─────────────┐  ┌─────────────┐  ┌──────────────────┐   │
│  │   NATS      │  │   MongoDB   │  │  S3/MinIO        │   │
│  │ (Backplane) │  │(Source of   │  │ (File Store)     │   │
│  │             │  │   Truth)    │  │                  │   │
│  └─────────────┘  └─────────────┘  └──────────────────┘   │
└─────────────────────────────────────────────────────────────┘
```

## Key Features

- **Native WebSocket Protocol**: Full-duplex communication without SignalR overhead
- **Live Voice Streaming**: Real-time binary audio chunks with in-memory buffering and assembly
- **File Sharing**: Upload/download with streaming S3 storage (images, documents, audio, video)
- **Presence & Typing**: Online/offline status per service with typing indicators
- **Service Join/Leave**: Clients join/leave service channels for scoped message delivery
- **Message Replies**: Thread-based conversations with reply context
- **Delivery Acknowledgments**: Ack/delivered receipts for message confirmation
- **Ping/Pong Heartbeat**: Connection health monitoring with idle timeout detection
- **CORS Support**: Configurable allowed origins for cross-domain access
- **Horizontal Scaling**: NATS backplane for cross-pod message delivery
- **Kubernetes Ready**: Complete manifests with HPA, PDB, and health checks

## Technology Stack

| Component | Technology |
|-----------|-----------|
| Framework | .NET 8 |
| WebSockets | System.Net.WebSockets |
| Message Bus | NATS Core |
| Database | MongoDB |
| Presence/Rate Limit | MongoDB |
| Voice Buffering | In-Memory (ConcurrentDictionary) |
| Storage | S3 / MinIO |
| Auth | JWT Bearer (header + query string) |
| Deployment | Kubernetes |
| Metrics | OpenTelemetry + Prometheus |

## Quick Start

### Prerequisites

- .NET 8 SDK
- Docker & Docker Compose
- Kubernetes cluster (for deployment)

### Local Development

1. Copy environment configuration:
```bash
cp .env.example .env
# Edit .env with your settings (JWT_SIGNING_KEY is required)
```

2. Start infrastructure services:
```bash
docker-compose up -d mongo nats minio
```

3. Run the API:
```bash
cd ChatHub.Api
dotnet run
```

The API starts at `http://localhost:5068` by default.

4. Connect via WebSocket:
```javascript
const ws = new WebSocket('ws://localhost:5068/ws?token=YOUR_JWT');
ws.onopen = () => console.log('Connected');
ws.onmessage = (e) => console.log('Received:', e.data);
```

### Docker Deployment

```bash
docker build -t chathub/api .
docker run -p 8080:8080 chathub/api
```

### Kubernetes Deployment

```bash
kubectl apply -f k8s/
```

## WebSocket Protocol

### Authentication

Include JWT token in the query string when connecting:
```
ws://api.chathub.example.com/ws?token=YOUR_JWT_TOKEN
```

The server sends periodic `ping` messages; clients must respond with `pong` to maintain the connection.

### Client → Server Messages

**Join Service:**
```json
{
  "type": "join_service",
  "serviceId": "service-123"
}
```

**Leave Service:**
```json
{
  "type": "leave_service",
  "serviceId": "service-123"
}
```

**Text Message:**
```json
{
  "type": "text_message",
  "id": "msg-uuid",
  "conversationId": "conv-123",
  "serviceId": "service-123",
  "text": "Hello, World!",
  "replyToId": "optional-parent-msg-id"
}
```

**Voice Chunk (Binary):**
```json
{
  "type": "voice_chunk",
  "id": "session-uuid",
  "conversationId": "conv-123",
  "sequenceNumber": 0,
  "isFinal": false
}
```
Voice chunks are also sent as binary WebSocket frames for efficiency.

**Voice Message (Complete):**
```json
{
  "type": "voice_message",
  "id": "msg-uuid",
  "conversationId": "conv-123",
  "blobId": "blob-id-from-upload",
  "durationMs": 5000,
  "mimeType": "audio/opus",
  "replyToId": "optional-parent-msg-id"
}
```

**File Attachment:**
```json
{
  "type": "file_attachment",
  "id": "msg-uuid",
  "conversationId": "conv-123",
  "blobId": "blob-id-from-upload",
  "fileName": "photo.jpg",
  "mimeType": "image/jpeg",
  "sizeBytes": 102400,
  "durationMs": null,
  "replyToId": "optional-parent-msg-id"
}
```

**Typing Indicator:**
```json
{
  "type": "typing",
  "conversationId": "conv-123",
  "isTyping": true
}
```

**Delivery Acknowledgment:**
```json
{
  "type": "ack",
  "messageId": "msg-uuid"
}
```

**Pong (Heartbeat Response):**
```json
{
  "type": "pong"
}
```

### Server → Client Messages

**Message Received:**
```json
{
  "type": "message_received",
  "envelope": {
    "id": "msg-uuid",
    "conversationId": "conv-123",
    "serviceId": "service-123",
    "senderId": "user-456",
    "type": "text",
    "text": "Hello, World!",
    "attachment": null,
    "replyToId": "parent-msg-id",
    "createdAt": "2024-01-15T10:30:00Z"
  }
}
```

The `envelope` includes an `attachment` field for voice/file messages:
```json
{
  "attachment": {
    "blobId": "blob-id",
    "fileName": "voice.opus",
    "mimeType": "audio/opus",
    "sizeBytes": 20480,
    "durationMs": 5000
  }
}
```

**Voice Chunk (Relayed):**
```json
{
  "type": "voice_chunk",
  "id": "session-uuid",
  "conversationId": "conv-123",
  "sequenceNumber": 0,
  "isFinal": false,
  "fromUserId": "user-456"
}
```

**User Joined:**
```json
{
  "type": "user_joined",
  "userId": "user-456",
  "serviceId": "service-123",
  "displayName": "John"
}
```

**User Left:**
```json
{
  "type": "user_left",
  "userId": "user-456",
  "serviceId": "service-123"
}
```

**Typing Indicator:**
```json
{
  "type": "typing",
  "userId": "user-456",
  "conversationId": "conv-123",
  "isTyping": true
}
```

**Delivery Receipt:**
```json
{
  "type": "delivered",
  "messageId": "msg-uuid"
}
```

**Error:**
```json
{
  "type": "error",
  "code": "rate_limit_exceeded",
  "message": "Rate limit exceeded",
  "correlationId": "corr-id"
}
```

**Ping (Heartbeat):**
```json
{
  "type": "ping"
}
```

## REST API

All REST endpoints require JWT Bearer authentication.

### Conversations

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | /api/conversation | List user's conversations |
| GET | /api/conversation/{id} | Get a single conversation |
| POST | /api/conversation | Create a conversation |
| GET | /api/conversation/{id}/messages | Get message history (paginated) |
| GET | /api/conversation/{id}/messages/{msgId}/replies | Get message thread |

**Create Conversation Request:**
```json
{
  "serviceId": "service-123",
  "title": "Optional title",
  "participantIds": ["user-456", "user-789"]
}
```

**Message History Query Parameters:**
- `before` - DateTime cursor for pagination
- `limit` - Max messages to return (default 50, max 100)

### Presence

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | /api/services/{serviceId}/online | List online users in a service |
| GET | /api/services/{serviceId}/online/{userId} | Check if a user is online |

### Files

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | /api/upload/file | Upload a file (multipart/form-data) |
| GET | /api/upload/download/{blobId} | Download a file (returns redirect to pre-signed URL or streams directly) |

**Upload Request:** `multipart/form-data` with `file` field and optional `durationMs` field (for voice/video).

**Upload Response:**
```json
{
  "blobId": "generated-uuid",
  "fileName": "photo.jpg",
  "mimeType": "image/jpeg",
  "sizeBytes": 102400,
  "durationMs": null
}
```

Maximum file size: 100 MB.

## Configuration

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `JWT_SIGNING_KEY` | JWT signing key (required) | - |
| `JWT_ISSUER` | JWT issuer | `ChatHub` |
| `JWT_AUDIENCE` | JWT audience | `ChatHub` |
| `MONGO_CONNECTION_STRING` | MongoDB connection string | `mongodb://localhost:27017/chathub` |
| `MONGO_DATABASE_NAME` | MongoDB database name | `chathub` |
| `NATS_URL` | NATS server URL | `nats://localhost:4222` |
| `NATS_QUEUE_GROUP` | NATS queue group name | `chathub-hub` |
| `S3_ENDPOINT` | S3/MinIO endpoint | `http://localhost:9000` |
| `S3_ACCESS_KEY` | S3 access key | `minioadmin` |
| `S3_SECRET_KEY` | S3 secret key | `minioadmin` |
| `S3_BUCKET` | S3 bucket name | `chathub-uploads` |
| `S3_REGION` | S3 region | `us-east-1` |
| `S3_FORCE_PATH_STYLE` | Force path-style S3 URLs | `true` |
| `CHATHUB_MAX_MESSAGE_SIZE_BYTES` | Max WebSocket message size | `65536` (64 KB) |
| `CHATHUB_PING_INTERVAL_SECONDS` | Ping interval for heartbeats | `15` |
| `CHATHUB_IDLE_TIMEOUT_MINUTES` | Idle connection timeout | `30` |
| `CHATHUB_RATE_LIMIT_TEXT_PER_MINUTE` | Text message rate limit | `100` |
| `CHATHUB_RATE_LIMIT_VOICE_PER_MINUTE` | Voice message rate limit | `10` |
| `CHATHUB_ALLOWED_ORIGINS` | CORS allowed origins (comma-separated) | `*` (any) |
| `POD_ID` | Pod identifier for NATS queue group | `unknown` |

## Monitoring

### Health Checks

- `/healthz` - Liveness probe (MongoDB + NATS checks)
- `/readyz` - Readiness probe

### Metrics

OpenTelemetry metrics exposed via the `ChatHub` meter:

| Metric | Description |
|--------|-------------|
| `chathub.messages.sent` | Messages sent by users |
| `chathub.messages.received` | Messages received by users |
| `chathub.messages.latency` | Message delivery latency (ms) |
| `chathub.connections.established` | WebSocket connections opened |
| `chathub.connections.closed` | WebSocket connections closed |
| `chathub.connections.duration` | WebSocket connection duration (s) |

### Logging

Structured logging with correlation IDs:
- Request tracing via `X-Correlation-Id` header
- WebSocket connection lifecycle logging
- Performance metrics per request

## Testing

### Unit Tests

```bash
dotnet test ChatHub.Tests --filter "FullyQualifiedName~Unit"
```

### Integration Tests

Requires running infrastructure services:
```bash
docker-compose up -d mongo nats minio
dotnet test ChatHub.Tests --filter "FullyQualifiedName~Integration"
```

## Project Structure

```
ChatHub/
├── ChatHub.Api/                  # Web API & WebSocket handlers
│   ├── Controllers/
│   │   ├── ConversationController.cs
│   │   ├── PresenceController.cs
│   │   └── UploadController.cs
│   ├── Handlers/                  # IMessageHandler<T> implementations
│   │   ├── AckHandler.cs
│   │   ├── DeliveredHandler.cs
│   │   ├── FileAttachmentHandler.cs
│   │   ├── JoinServiceHandler.cs
│   │   ├── LeaveServiceHandler.cs
│   │   ├── PongHandler.cs
│   │   ├── TextMessageHandler.cs
│   │   ├── TypingHandler.cs
│   │   ├── VoiceChunkHandler.cs
│   │   └── VoiceMessageHandler.cs
│   ├── HealthChecks/
│   │   ├── MongoHealthCheck.cs
│   │   └── NatsHealthCheck.cs
│   ├── Metrics/
│   │   └── ChatMetrics.cs
│   ├── Middleware/
│   │   ├── CorrelationIdMiddleware.cs
│   │   ├── WebSocketLoggingMiddleware.cs
│   │   └── WebSocketMiddleware.cs
│   ├── MessageDispatcher.cs
│   └── Program.cs
├── ChatHub.Core/                 # Domain models & interfaces
│   ├── Documents/
│   │   ├── ConnectionDocument.cs
│   │   ├── ConversationDocument.cs
│   │   ├── MessageDocument.cs
│   │   ├── PresenceDocument.cs
│   │   └── RateLimitDocument.cs
│   ├── Interfaces/
│   │   ├── IBlobStorageClient.cs
│   │   ├── IConnectionRegistry.cs
│   │   ├── IConversationRepository.cs
│   │   ├── IJwtValidator.cs
│   │   ├── IMessageDispatcher.cs
│   │   ├── IMessageRepository.cs
│   │   ├── INatsBackplane.cs
│   │   ├── IPresenceService.cs
│   │   ├── IRateLimiter.cs
│   │   ├── IVoiceSessionBuffer.cs
│   │   ├── IWebSocketConnection.cs
│   │   └── IWebSocketSender.cs
│   ├── Models/
│   │   ├── ClientMessage.cs
│   │   ├── MessageEnvelope.cs
│   │   └── ServerMessage.cs
│   └── Settings/
│       ├── ChatHubSettings.cs
│       ├── JwtSettings.cs
│       ├── MongoSettings.cs
│       ├── NatsSettings.cs
│       └── StorageSettings.cs
├── ChatHub.Infrastructure/       # External service implementations
│   ├── Auth/
│   │   └── JwtValidator.cs
│   ├── Cache/
│   │   ├── MongoDbPresenceService.cs
│   │   ├── MongoDbRateLimiter.cs
│   │   ├── VoiceSessionBuffer.cs
│   │   └── VoiceSessionCleanupService.cs
│   ├── Nats/
│   │   ├── NatsBackplane.cs
│   │   └── NatsSubscriberService.cs
│   ├── Persistence/
│   │   ├── ConversationRepository.cs
│   │   ├── MessageRepository.cs
│   │   └── MongoInitializer.cs
│   ├── Storage/
│   │   └── S3BlobStorageClient.cs
│   ├── WebSockets/
│   │   ├── ConnectionRegistry.cs
│   │   ├── WebSocketConnection.cs
│   │   └── WebSocketSender.cs
│   └── Writers/
│       └── MongoWriterService.cs
├── ChatHub.Tests/                 # Test projects
│   ├── Integration/
│   │   ├── MongoTests.cs
│   │   ├── NatsTests.cs
│   │   └── WebSocketTests.cs
│   └── Unit/
│       ├── Handlers/
│       │   └── MessageSerializationTests.cs
│       └── WebSockets/
│           └── ConnectionRegistryTests.cs
├── k8s/                           # Kubernetes manifests
│   ├── configmap.yaml
│   ├── deployment.yaml
│   ├── hpa.yaml
│   ├── ingress.yaml
│   ├── nats-values.yaml
│   ├── pdb.yaml
│   ├── secret.yaml
│   └── service.yaml
├── docker-compose.yml
├── Dockerfile
├── .env.example
└── specs/                         # Specifications
```

## License

MIT