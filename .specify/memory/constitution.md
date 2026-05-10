<!--
Sync Impact Report
Version change: 0.0.0 → 1.0.0 (initial ratification)
Modified principles: N/A (initial creation)
Added sections: All sections (initial creation)
Removed sections: N/A
Templates requiring updates:
  - ✅ plan-template.md: No changes needed - aligns with Core Principles
  - ✅ spec-template.md: No changes needed - follows Test-First principle
  - ✅ tasks-template.md: No changes needed - supports Implementation Quality
Follow-up TODOs: None - all placeholders filled
-->

# ChatHub Constitution

## Core Principles

### I. WebSocket-First Real-time Communication
All client connections use native WebSocket protocol with a custom JSON wire format. No SignalR or abstraction layers between server and raw WebSocket frames.

**Rationale**: Direct WebSocket control enables minimal latency, cross-platform compatibility (web, iOS, Android without SDK dependencies), and explicit protocol design. This is non-negotiable for a real-time chat service.

### II. MongoDB as Source of Truth
MongoDB owns all durability. Every message is persisted to MongoDB before any NATS publish. NATS core is at-most-once delivery only — no message replay, no JetStream. Clients fetch missed history from MongoDB on reconnect.

**Rationale**: Separating durability (MongoDB) from delivery (NATS) simplifies the architecture and ensures consistent state across pod restarts. NATS is purely for real-time fan-out, not persistence.

### III. NATS Core for Cross-Pod Fan-out
Use NATS core pub/sub with queue groups for load-balanced delivery across pods. No JetStream, no streams, no consumers. Fire-and-forget semantics acceptable — durability is MongoDB's responsibility.

**Rationale**: NATS core provides exactly-once delivery within queue groups with minimal overhead. The at-most-once semantics are acceptable because MongoDB persists all messages; NATS only needs to deliver to currently connected pods.

### IV. Layered Architecture with Clear Boundaries
Maintain strict separation between layers:
- **ChatHub.Core**: Models, DTOs, interfaces, settings — no external dependencies
- **ChatHub.Infrastructure**: Implementations (WebSockets, NATS, MongoDB, S3)
- **ChatHub.Api**: Middleware, controllers, DI wiring — thin layer only

**Rationale**: Clean architecture enables testability, allows infrastructure swapping, and prevents business logic leakage into transport concerns.

### V. Background Services for I/O Offloading
All blocking or slow I/O (MongoDB writes, NATS publishes, S3 uploads) happen in dedicated BackgroundService implementations with Channel-based queuing. Never block the hot receive path.

**Rationale**: WebSocket receive loops must remain responsive. Offloading to channels with background drainers prevents backpressure and maintains throughput under load.

## Technology Stack Requirements

### Required Infrastructure
- **.NET 8+**: ASP.NET Core with native WebSocket middleware
- **MongoDB 7+**: Official .NET driver, no ODM abstraction
- **NATS Server**: Core pub/sub only, no JetStream, 3-node cluster
- **In-Memory Cache**: Presence, session state, rate limiting, voice chunk assembly (pod-local)
- **S3-Compatible Storage**: MinIO for local, AWS S3 for production

### Development Standards
- No Entity Framework Core — use MongoDB driver directly
- No SignalR — native WebSocket only
- System.Text.Json for all serialization (camelCase)
- Async/await throughout — no blocking calls
- Structured logging with correlation IDs

## Operational Requirements

### Deployment
- Kubernetes with HPA (3-20 replicas based on CPU + custom metric)
- PodDisruptionBudget with minAvailable: 2
- Graceful shutdown: 5s preStop sleep for WebSocket drain
- Ingress with extended timeouts (3600s) for WebSocket support

### Monitoring
- Health checks for NATS, MongoDB
- Metrics: active connections, message throughput, latency percentiles
- Distributed tracing across pod boundaries

## Governance

This constitution supersedes all other development practices. All code changes must:
1. Follow the layered architecture — no infrastructure code in Core or Api
2. Persist to MongoDB before NATS publish
3. Use queue groups for NATS subscription (except broadcast topics)
4. Include unit tests for handlers and integration tests for WebSocket/NATS/MongoDB
5. Not introduce blocking I/O in receive loops

Amendments require documentation update, explicit approval, and migration plan for existing code.

**Version**: 1.0.0 | **Ratified**: 2026-05-09 | **Last Amended**: 2026-05-09
