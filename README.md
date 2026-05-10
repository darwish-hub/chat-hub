# ChatHub - Real-Time Chat Service

A high-performance real-time chat service built with .NET 8, featuring native WebSocket support, live voice messaging, file sharing, presence tracking, and message replies.

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                        Clients                               │
│  (WebSocket / REST API)                                     │
└──────────────────────┬──────────────────────────────────────┘
                       │ HTTPS/WSS
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
│  │   NATS      │  │   MongoDB   │   │
│  │ (Backplane) │  │  (Source of │   │
│  │             │  │             │  │   Truth)         │   │
│  └─────────────┘                  └──────────────────┘   │
│         │                                        │          │
│         └────────────────┬───────────────────────┘          │
│                          │                                  │
│                   ┌──────▼──────┐                          │
│                   │  S3/MinIO   │                          │
│                   │(File Store) │                          │
│                   └─────────────┘                          │
└─────────────────────────────────────────────────────────────┘
```

## Key Features

- **Native WebSocket Protocol**: Full-duplex communication without SignalR overhead
- **Live Voice Streaming**: Real-time audio chunks with buffering and assembly
- **File Sharing**: Upload/download with streaming S3 storage
- **Presence & Typing**: Online/offline status with typing indicators
- **Message Replies**: Thread-based conversations with reply context
- **Horizontal Scaling**: NATS backplane for cross-pod message delivery
- **Kubernetes Ready**: Complete manifests with HPA, PDB, and health checks

## Technology Stack

| Component | Technology |
|-----------|-----------|
| Framework | .NET 8 |
| WebSockets | System.Net.WebSockets |
| Message Bus | NATS Core |
| Database | MongoDB |
| Cache | MongoDB (presence/rate limit) + In-Memory (voice) |
| Storage | S3 / MinIO |
| Auth | JWT Bearer |
| Deployment | Kubernetes |
| Metrics | OpenTelemetry + Prometheus |

## Quick Start

### Prerequisites

- .NET 8 SDK
- Docker & Docker Compose
- Kubernetes cluster (for deployment)

### Local Development

1. Start infrastructure services:
```bash
docker-compose up -d mongodb nats minio
```

2. Run the API:
```bash
cd ChatHub.Api
dotnet run
```

3. Connect via WebSocket:
```javascript
const ws = new WebSocket('ws://localhost:5123/ws?token=YOUR_JWT');
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

Include JWT token in query string:
```
ws://api.chathub.example.com/ws?token=YOUR_JWT_TOKEN
```

### Client → Server Messages

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

**Voice Chunk (Live Streaming):**
```json
{
  "type": "voice_chunk",
  "id": "session-uuid",
  "conversationId": "conv-123",
  "chunkIndex": 0,
  "isLast": false
}
```

**Join Service:**
```json
{
  "type": "join_service",
  "serviceId": "service-123"
}
```

### Server → Client Messages

**Message Received:**
```json
{
  "type": "message_received",
  "envelope": {
    "id": "msg-uuid",
    "type": "text",
    "text": "Hello, World!",
    "replyToId": "parent-msg-id",
    "createdAt": "2024-01-15T10:30:00Z"
  }
}
```

**Typing Indicator:**
```json
{
  "type": "typing",
  "userId": "user-123",
  "conversationId": "conv-123",
  "isTyping": true
}
```

## REST API

### Conversations

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | /api/conversations | Create conversation |
| GET | /api/conversations/{id}/messages | Get message history |
| GET | /api/conversations/{id}/messages/{msgId}/replies | Get thread |

### Presence

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | /api/services/{id}/online | List online users |
| GET | /api/services/{id}/online/{userId} | Check user status |

### Files

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | /api/files/upload | Upload file |
| GET | /api/files/{blobId} | Download file |

## Configuration

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `MONGO_CONNECTION_STRING` | MongoDB connection string | `mongodb://localhost:27017/chathub` |
| `NATS_URL` | NATS server URL | `nats://localhost:4222` |
| `S3_ENDPOINT` | S3/MinIO endpoint | `http://localhost:9000` |
| `JWT_SIGNING_KEY` | JWT secret key | (required) |
| `CHATHUB_RATE_LIMIT_TEXT_PER_MINUTE` | Text message rate limit | 100 |
| `CHATHUB_RATE_LIMIT_VOICE_PER_MINUTE` | Voice message rate limit | 10 |

## Monitoring

### Health Checks

- `/health` - Liveness probe
- `/health/ready` - Readiness probe

### Metrics

Prometheus metrics exposed at `/metrics`:

- `chathub_messages_sent` - Messages sent by users
- `chathub_messages_received` - Messages received by users
- `chathub_messages_latency` - Message delivery latency
- `chathub_connections_established` - WebSocket connections opened
- `chathub_connections_closed` - WebSocket connections closed
- `chathub_connections_duration` - Connection duration

### Logging

Structured logging with correlation IDs:
- Request tracing via `X-Correlation-Id` header
- WebSocket connection lifecycle logging
- Performance metrics per request

## Testing

### Unit Tests

```bash
dotnet test ChatHub.Tests/Unit
```

### Integration Tests

Requires running infrastructure services:
```bash
dotnet test ChatHub.Tests/Integration
```

## Project Structure

```
ChatHub/
├── ChatHub.Api/              # Web API & WebSocket handlers
│   ├── Controllers/          # REST API controllers
│   ├── Handlers/             # WebSocket message handlers
│   ├── Middleware/           # HTTP middleware
│   └── Program.cs            # Application entry point
├── ChatHub.Core/             # Domain models & interfaces
│   ├── Documents/            # MongoDB documents
│   ├── Interfaces/           # Repository & service interfaces
│   ├── Models/               # Message models
│   └── Settings/             # Configuration settings
├── ChatHub.Infrastructure/   # External service implementations
│   ├── Cache/                # In-memory cache implementations
│   ├── Nats/                 # NATS backplane
│   ├── Persistence/          # MongoDB repositories
│   ├── Storage/              # S3/MinIO storage
│   ├── WebSockets/           # WebSocket management
│   └── Writers/              # Channel-based writers
├── ChatHub.Tests/            # Test projects
│   ├── Integration/          # Integration tests
│   └── Unit/                 # Unit tests
├── k8s/                      # Kubernetes manifests
└── specs/                    # Specifications
```

## License

MIT
