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
│   │   └── UploadController.cs
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
