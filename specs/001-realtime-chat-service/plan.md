# Implementation Plan: Real-time Chat Service

**Branch**: `001-realtime-chat-service` | **Date**: 2026-05-09 | **Spec**: [specs/001-realtime-chat-service/spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-realtime-chat-service/spec.md`

## Summary

Build a high-performance real-time chat service with native WebSockets, NATS Core pub/sub backplane, MongoDB persistence, in-memory presence/rate limiting, and in-memory voice buffering. The service supports text messaging, live voice streaming, file sharing, presence indicators, and message threading across web, iOS, and Android clients.

## Technical Context

**Language/Version**: .NET 8+ with C# 12
**Primary Dependencies**:
- ASP.NET Core native WebSocket middleware
- NATS core .NET client
- MongoDB official .NET driver
- AWS S3/MinIO SDK

**Storage**: MongoDB 7+ (primary persistence, presence, rate limiting), S3-compatible storage (blobs), pod-local memory (voice chunk buffering)

**Testing**: xUnit with Testcontainers (MongoDB, NATS), WebApplicationFactory for integration tests

**Target Platform**: Kubernetes-deployed microservice with horizontal pod autoscaling

**Project Type**: Web service with real-time communication capabilities

**Performance Goals**:
- Message delivery < 1 second (99% of cases)
- Voice streaming latency < 500ms
- Support 10,000 concurrent connections
- 100 messages/minute per user without rate limiting

**Constraints**:
- No SignalR - native WebSocket only
- No Entity Framework - MongoDB driver directly
- No NATS JetStream - core pub/sub only
- Background services must use Channel-based queuing

**Scale/Scope**: Multi-pod deployment with cross-pod message fan-out, 3-20 replicas, 3-node NATS cluster

## Known Issues Fixed

### Message Delivery Bug (Fixed 2026-05-12)
- **Symptom**: Messages were saved to MongoDB but not received by other users
- **Root Cause**: Web client only passed `[myUserId]` when creating conversations, excluding other participants
- **Fix**: Modified web client to prompt for participant IDs on conversation creation, added "Browse Conversations" feature
- **Related Files**:
  - `ChatHub.Api/Controllers/ConversationController.cs` - Added `JoinConversation` and `GetAvailableConversations` endpoints
  - `ChatHub.Core/Interfaces/IConversationRepository.cs` - Added `JoinConversationAsync`
  - `ChatHub.Infrastructure/Persistence/ConversationRepository.cs` - Implemented join functionality

### Message Received Frame Validation Bug (Fixed 2026-05-12)
- **Symptom**: `Invalid frame structure, discarding: message_received` in web client logs
- **Root Cause**: `parseTextFrame` unwrapped `frame.envelope` into flat fields, but `validateServerFrame` still checked for `frame.envelope`
- **Fix**: Updated validator in `parsers.js` to validate flat structure after unwrapping

### JWT User ID Field (Updated 2026-05-12)
- **Change**: Web client now uses `nid` (National ID) field from JWT payload, falling back to `sub` if not present
- **Related Files**:
  - `chat-client-web/src/ui/MessageList.jsx` - Updated user ID extraction

### Conversation Creation & Joining Flow

**Problem**: Users needed a way to create conversations with multiple participants and join existing ones.

**Solution**:
1. **Create Conversation**: User enters title and comma-separated participant IDs (including their own)
2. **Browse Conversations**: GET `/api/conversation/available` returns all conversations in a service
3. **Join Conversation**: POST `/api/conversation/{id}/join` adds user to conversation participants

**REST API Endpoints**:
- `GET /api/conversation/available?serviceId={serviceId}` - List available conversations
- `POST /api/conversation/{id}/join` - Join a conversation
- `POST /api/conversation` - Create new conversation (existing)

**Key Files**:
- `ChatHub.Api/Controllers/ConversationController.cs` - Added JoinConversation and GetAvailableConversations
- `ChatHub.Core/Interfaces/IConversationRepository.cs` - Added GetAllAsync, JoinConversationAsync
- `ChatHub.Infrastructure/Persistence/ConversationRepository.cs` - Implemented new repository methods

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Principle I: WebSocket-First Real-time Communication ✅
- **Status**: COMPLIANT
- **Implementation**: Native WebSocket middleware at `/ws` endpoint with custom JSON wire format
- **No SignalR**: Direct WebSocket control for minimal latency

### Principle II: MongoDB as Source of Truth ✅
- **Status**: COMPLIANT
- **Implementation**: All messages persisted to MongoDB before NATS publish
- **No JetStream**: Clients fetch missed history from MongoDB on reconnect

### Principle III: NATS Core for Cross-Pod Fan-out ✅
- **Status**: COMPLIANT
- **Implementation**: Queue groups for load-balanced delivery, fire-and-forget semantics
- **No streams/consumers**: NATS purely for real-time fan-out

### Principle IV: Layered Architecture with Clear Boundaries ✅
- **Status**: COMPLIANT
- **Implementation**:
  - ChatHub.Core: Models, DTOs, interfaces, settings
  - ChatHub.Infrastructure: WebSockets, NATS, MongoDB, S3 implementations
  - ChatHub.Api: Middleware, controllers, DI wiring

### Principle V: Background Services for I/O Offloading ✅
- **Status**: COMPLIANT
- **Implementation**: Channel-based queuing for MongoDB writes, NATS publishes, S3 uploads
- **No blocking**: WebSocket receive loops remain non-blocking

## Project Structure

### Documentation (this feature)

```text
specs/001-realtime-chat-service/
├── spec.md              # Feature specification
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── websocket-protocol.md
│   └── rest-api.md
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
ChatHub.sln
├── ChatHub.Api/
│   ├── Program.cs
│   ├── Middleware/
│   │   └── WebSocketMiddleware.cs
│   ├── Handlers/                      # IMessageHandler<T> implementations
│   ├── Controllers/
│   │   ├── UploadController.cs
│   │   └── ConversationController.cs   # REST API for conversations
│   └── HealthChecks/
│       ├── NatsHealthCheck.cs
│       └── MongoHealthCheck.cs
├── ChatHub.Core/
│   ├── Models/                        # ClientMessage, ServerMessage, MessageEnvelope DTOs
│   ├── Documents/                     # MessageDocument (unified attachment), ConversationDocument
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
│   │   └── NatsSubscriberService.cs   # BackgroundService
│   ├── Persistence/
│   │   ├── MongoInitializer.cs        # IHostedService
│   │   ├── MessageRepository.cs
│   │   └── ConversationRepository.cs
│   ├── Writers/
│   │   └── MongoWriterService.cs      # BackgroundService
│   ├── Cache/
│   │   ├── VoiceSessionBuffer.cs           # In-memory voice chunk storage
│   │   ├── VoiceSessionCleanupService.cs   # BackgroundService
│   │   ├── MongoDbPresenceService.cs
│   │   └── MongoDbRateLimiter.cs
│   └── Storage/
│       └── S3BlobStorageClient.cs
├── ChatHub.Tests/
│   ├── Unit/
│   │   ├── Handlers/
│   │   └── WebSockets/
│   └── Integration/
│       ├── WebSocketTests.cs
│       ├── NatsTests.cs
│       └── MongoTests.cs
└── k8s/                               # Kubernetes manifests
    ├── deployment.yaml
    ├── service.yaml
    ├── ingress.yaml
    ├── hpa.yaml
    ├── pdb.yaml
    ├── configmap.yaml
    ├── secret.yaml
    └── nats-values.yaml
```

### Web Test Client (chat-client-web/)

```text
chat-client-web/
├── src/
│   ├── App.jsx                    # Main app with 4-column layout
│   ├── App.css                    # Styling
│   ├── main.jsx
│   ├── index.html
│   ├── transport/
│   │   ├── wsClient.js            # WebSocket client with reconnection
│   │   ├── sendQueue.js
│   │   └── heartbeat.js
│   ├── protocol/
│   │   ├── parsers.js             # Frame parsing and validation
│   │   ├── builders.js            # Message builders
│   │   └── messageTypes.js
│   ├── api/
│   │   ├── conversations.js       # REST API for conversations
│   │   ├── history.js
│   │   ├── presence.js
│   │   ├── upload.js
│   │   └── download.js
│   ├── state/
│   │   ├── conversationStore.js
│   │   ├── messageStore.js
│   │   ├── presenceStore.js
│   │   └── voiceSessionStore.js
│   └── ui/
│       ├── AuthPanel.jsx
│       ├── ConversationList.jsx
│       ├── MessageList.jsx
│       ├── Composer.jsx
│       ├── VoiceRecorder.jsx
│       ├── PresenceBar.jsx
│       ├── TypingIndicator.jsx
│       ├── ProtocolLog.jsx
│       ├── MetricsDashboard.jsx
│       └── TestScenarios.jsx
└── package.json
```

### Web Client UI Layout

The web client uses a 4-column responsive layout:

```
┌────────────────────────────────────────────────────────────────────┐
│ Header: Title + Status + Controls                                   │
├──────────┬──────────────────────┬──────────┬─────────────────────┤
│CONVERSATIONS│    MESSAGES      │ METRICS  │    LOGS & TRACE    │
│            │                  │          │                     │
│ [Browse]  │                  │  TEST    │                     │
│            │                  │  SCENARIOS│                     │
│────────────│                  │          │                     │
│ PRESENCE  │                  │          │                     │
└──────────┴──────────────────────┴──────────┴─────────────────────┘
```

- **Column 1** (180px): Conversations list + Presence
- **Column 2** (30%): Messages area with composer
- **Column 3** (140px): Metrics + Test scenarios
- **Column 4** (flex): Logs & Trace

**Connect Screen**: Separate full-page layout for JWT authentication before accessing the main chat interface.

## Complexity Tracking

> No Constitution violations identified. All principles align with feature requirements.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| N/A | N/A | N/A |

## Phase 0: Research Completed

All technical decisions validated against Constitution:
- WebSocket protocol design maintains direct frame control
- MongoDB write-before-publish pattern ensures durability
- NATS queue groups enable horizontal scaling
- Layered architecture supports testing and evolution
- Channel-based background services prevent blocking

## Phase 1: Design Complete

### Data Model
See [data-model.md](./data-model.md) for entity definitions and relationships.

### Contracts
See [contracts/](./contracts/) for:
- WebSocket wire protocol specification
- REST API endpoints for file uploads

### Quick Start
See [quickstart.md](./quickstart.md) for local development setup.

---

**Status**: Ready for task generation (`/speckit.tasks`)
