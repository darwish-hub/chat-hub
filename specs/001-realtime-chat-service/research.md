# Research: Real-time Chat Service Implementation

**Date**: 2026-05-09  
**Purpose**: Validate technical decisions and resolve implementation approaches

## Research Findings

### WebSocket Protocol Design

**Decision**: Native ASP.NET Core WebSocket middleware with custom JSON wire format

**Rationale**:
- Direct control over frame handling for minimal latency
- Cross-platform compatibility without SignalR client SDK dependencies
- Enables binary frame support for voice streaming
- Allows custom heartbeat and connection management

**Alternatives Considered**:
- SignalR: Rejected due to abstraction layer and client SDK requirements
- Socket.IO: Rejected due to JavaScript-centric design and additional protocol overhead
- gRPC-Web: Rejected due to limited browser support and binary framing complexity

**Key Insights**:
- WebSocket frame size limited to 4KB for initial buffer, expandable via ArrayPool
- Must handle fragmented frames for large messages
- Binary frames ideal for voice chunk streaming
- Query parameter token passing works across all platforms for auth

### Message Backplane (Cross-Pod Communication)

**Decision**: NATS Core pub/sub with queue groups

**Rationale**:
- Queue groups ensure exactly-once delivery within the group
- Fire-and-forget semantics acceptable (MongoDB owns durability)
- Lower operational complexity than JetStream
- Excellent performance for fan-out scenarios

**Alternatives Considered**:
- Redis Pub/Sub: Rejected - no queue groups, delivery to all subscribers
- RabbitMQ: Rejected - higher operational complexity, overkill for this use case
- Kafka: Rejected - designed for log streaming, not real-time messaging
- NATS JetStream: Rejected per Constitution - not needed, MongoDB handles durability

**Key Insights**:
- Queue group name: `chathub-hub`
- Subject pattern: `chathub.{serviceId}.messages`, `chathub.{serviceId}.presence`
- Broadcast subjects (no queue group): `chathub.system.broadcast`
- At-most-once delivery acceptable - clients fetch history from MongoDB

### Persistence Strategy

**Decision**: MongoDB 7+ with official .NET driver

**Rationale**:
- Document model fits message and conversation structures naturally
- Flexible schema supports message type variations (text, voice, file)
- Strong consistency for source-of-truth responsibility
- Horizontal scaling via sharding if needed

**Alternatives Considered**:
- PostgreSQL with JSONB: Rejected - adds relational complexity unnecessary for chat
- Cassandra: Rejected - eventual consistency conflicts with source-of-truth requirement
- DynamoDB: Rejected - vendor lock-in, complex partition key design for chat patterns

**Key Insights**:
- TTL indexes for ephemeral data (connection logs)
- Compound indexes: `{ conversationId: 1, createdAt: -1 }`
- Background service with Channel for write queuing
- InsertOneAsync before NATS publish

### Caching Layer

**Decision**: Redis 7+ for presence, rate limiting, and voice chunk assembly

**Rationale**:
- Sorted sets perfect for voice chunk sequencing
- INCR/EXPIRE pattern ideal for sliding window rate limiting
- Pub/sub not used (NATS handles that)
- Fast ephemeral storage for session state

**Use Cases**:
- Presence: `presence:{serviceId}` hash set
- Rate limiting: `ratelimit:{connectionId}` with TTL
- Voice assembly: `voice:{messageId}` sorted set by sequence number

### File Storage

**Decision**: S3-compatible storage (MinIO local, AWS S3 production)

**Rationale**:
- Standard object storage API
- Supports streaming uploads/downloads
- Cost-effective for blob storage
- MinIO provides local development parity

**Implementation Notes**:
- Direct stream to S3 without buffering in memory
- Pre-signed URLs for client downloads
- Metadata stored in MongoDB, blobId references S3 object

### Authentication Approach

**Decision**: JWT validation on WebSocket handshake

**Rationale**:
- Stateless authentication scales horizontally
- Industry standard for modern APIs
- Can extract claims for authorization decisions

**Implementation**:
- Token via query parameter `/ws?token={jwt}` (HTTP upgrade limitation)
- Validate before AcceptWebSocketAsync
- Reject with HTTP 401 if invalid
- Check token expiry in heartbeat loop

### Rate Limiting Strategy

**Decision**: Redis-based sliding window counters

**Rationale**:
- Distributed rate limiting across pods
- Sliding window smoother than fixed window
- Redis atomic operations prevent race conditions

**Limits**:
- 100 text/file messages per connection per minute
- 10 voice messages per connection per minute

### Deployment Architecture

**Decision**: Kubernetes with HPA

**Rationale**:
- Horizontal pod autoscaling handles connection spikes
- PodDisruptionBudget ensures availability during rollouts
- NATS cluster runs as 3-node StatefulSet
- PreStop hook for graceful WebSocket drain

**Scaling Metrics**:
- CPU: 70% target
- Custom metric: `active_websocket_connections` > 8000 per pod

## Decisions Summary

| Component | Choice | Key Reason |
|-----------|--------|------------|
| Transport | Native WebSocket | Direct control, no SignalR |
| Backplane | NATS Core | Queue groups, low overhead |
| Persistence | MongoDB | Document model, source of truth |
| Cache | Redis | Presence, rate limiting, voice assembly |
| Storage | S3-compatible | Blob storage, streaming |
| Auth | JWT | Stateless, scalable |
| Rate Limit | Redis sliding window | Distributed, fair |
| Deployment | Kubernetes HPA | Auto-scaling, resilient |

## Constitution Alignment

All decisions align with the 5 core principles:

1. **WebSocket-First**: Native implementation, no SignalR
2. **MongoDB Source of Truth**: Persistence before publish, history on reconnect
3. **NATS Core**: Queue groups, no JetStream
4. **Layered Architecture**: Core/Infrastructure/Api separation
5. **Background Services**: Channel-based I/O offloading

No clarifications needed - architecture validated.
