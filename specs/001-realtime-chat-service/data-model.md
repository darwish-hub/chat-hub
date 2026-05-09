# Data Model: Real-time Chat Service

**Date**: 2026-05-09  
**Purpose**: Define entities, fields, relationships, and validation rules

## Entity Relationship Diagram

```
┌─────────────────┐     ┌──────────────────┐     ┌─────────────────┐
│     User        │     │   Conversation   │     │     Message     │
├─────────────────┤     ├──────────────────┤     ├─────────────────┤
│ _id (ObjectId)  │◄────┤ _id (ObjectId)   │◄────┤ _id (ObjectId)  │
│ userId (string) │     │ serviceId        │     │ conversationId  │
│ displayName     │     │ participantIds[] │     │ senderId        │
│ email           │     │ createdAt        │     │ type            │
│ createdAt       │     │ lastMessageAt    │     │ text            │
└─────────────────┘     └──────────────────┘     │ voice {blobId}  │
                                                 │ file {blobId}   │
┌─────────────────┐                              │ replyToId       │
│   Connection    │                              │ createdAt       │
├─────────────────┤                              │ deliveredAt     │
│ _id (ObjectId)  │                              └─────────────────┘
│ userId          │
│ connectionId    │     ┌──────────────────┐
│ serviceId       │     │      File        │
│ podId           │     ├──────────────────┤
│ connectedAt     │     │ blobId           │
│ disconnectedAt  │     │ fileName         │
└─────────────────┘     │ mimeType         │
                        │ sizeBytes        │
                        │ uploadedBy       │
                        │ uploadedAt       │
                        └──────────────────┘
```

## Entities

### User

Represents a person using the chat service.

**Fields**:
- `_id` (ObjectId): Primary key
- `userId` (string): External identity provider ID (unique, indexed)
- `displayName` (string): Human-readable name shown in UI
- `email` (string): Contact email from identity provider
- `createdAt` (ISODate): Account creation timestamp
- `updatedAt` (ISODate): Last profile update

**Validation Rules**:
- `userId`: Required, non-empty, max 255 chars
- `displayName`: Required, 1-100 chars
- `email`: Valid email format per RFC 5322

**Indexes**:
- `{ userId: 1 }` - unique

---

### Conversation

A chat channel containing messages.

**Fields**:
- `_id` (ObjectId): Primary key
- `serviceId` (string): Logical grouping identifier
- `participantIds` (string[]): Array of userIds who can access
- `title` (string | null): Optional conversation name
- `createdAt` (ISODate): Creation timestamp
- `lastMessageAt` (ISODate): Timestamp of most recent message
- `createdBy` (string): userId of creator

**Validation Rules**:
- `serviceId`: Required, non-empty, max 255 chars
- `participantIds`: Required, at least 2 participants, max 500
- `title`: Optional, max 200 chars

**Indexes**:
- `{ serviceId: 1 }`
- `{ participantIds: 1 }`
- `{ serviceId: 1, lastMessageAt: -1 }`

---

### Message

A unit of communication (text, voice, or file).

**Fields**:
- `_id` (ObjectId): Primary key
- `conversationId` (string): Reference to conversation
- `serviceId` (string): Denormalized for querying
- `senderId` (string): userId of sender
- `type` (string enum): `"text" | "voice" | "file"`
- `text` (string | null): Text content (when type="text")
- `voice` (object | null): Voice metadata (when type="voice")
  - `blobId` (string): S3 object reference
  - `durationMs` (int): Audio length in milliseconds
  - `mimeType` (string): Audio format (e.g., "audio/opus")
- `file` (object | null): File metadata (when type="file")
  - `blobId` (string): S3 object reference
  - `fileName` (string): Original filename
  - `mimeType` (string): File MIME type
  - `sizeBytes` (int): File size
- `replyToId` (string | null): Reference to parent message
- `createdAt` (ISODate): Message timestamp
- `deliveredAt` (ISODate | null): When all participants received

**Validation Rules**:
- `conversationId`: Required, valid conversation reference
- `senderId`: Required, must be in conversation participantIds
- `type`: Required, one of ["text", "voice", "file"]
- Exactly one of `text`, `voice`, `file` must be non-null based on type
- `text` (if present): Required, max 10,000 chars
- `voice.durationMs` (if present): Required, > 0, max 300,000 (5 min)
- `file.sizeBytes` (if present): Required, > 0, max 104,857,600 (100 MB)
- `replyToId` (if present): Must reference existing message in same conversation

**Indexes**:
- `{ conversationId: 1, createdAt: -1 }` - primary query pattern
- `{ serviceId: 1, createdAt: -1 }`
- `{ senderId: 1, createdAt: -1 }`

---

### Connection (Ephemeral)

Tracks active WebSocket connections for audit and analytics.

**Fields**:
- `_id` (ObjectId): Primary key
- `userId` (string): Connected user
- `connectionId` (string): WebSocket connection GUID
- `serviceId` (string | null): Service joined (if any)
- `podId` (string): Kubernetes pod hostname
- `clientInfo` (object):
  - `ipAddress` (string): Client IP
  - `userAgent` (string): Client user agent
- `connectedAt` (ISODate): Connection established
- `disconnectedAt` (ISODate | null): Connection closed

**Validation Rules**:
- `userId`: Required
- `connectionId`: Required, valid GUID format
- `podId`: Required

**Indexes**:
- `{ userId: 1, connectedAt: -1 }`
- `{ connectionId: 1 }`
- TTL index on `disconnectedAt` with 24-hour expiry

---

### FileMetadata

Metadata for uploaded files stored in S3.

**Fields**:
- `blobId` (string): Unique S3 object key (UUID v4)
- `fileName` (string): Original filename
- `mimeType` (string): MIME type
- `sizeBytes` (int): File size
- `uploadedBy` (string): userId of uploader
- `uploadedAt` (ISODate): Upload timestamp
- `expiresAt` (ISODate | null): Optional expiration

**Validation Rules**:
- `blobId`: Required, valid UUID v4
- `fileName`: Required, max 255 chars
- `mimeType`: Required, valid MIME type
- `sizeBytes`: Required, > 0, max 104,857,600 (100 MB)

**Indexes**:
- `{ blobId: 1 }` - unique
- `{ uploadedBy: 1, uploadedAt: -1 }`

## State Transitions

### Message Lifecycle

```
┌─────────────┐    ┌─────────────┐    ┌─────────────┐    ┌─────────────┐
│  Sending    │───►│  Persisted  │───►│  Delivered  │───►│   Read      │
│  (client)   │    │  (MongoDB)  │    │  (all got)  │    │  (viewed)   │
└─────────────┘    └─────────────┘    └─────────────┘    └─────────────┘
```

1. **Sending**: Client sends message to server
2. **Persisted**: Message written to MongoDB (source of truth)
3. **Delivered**: All active participants received via WebSocket
4. **Read**: Recipient viewed the message (optional future enhancement)

### Connection Lifecycle

```
┌─────────────┐    ┌─────────────┐    ┌─────────────┐
│  Connected  │───►│   Joined    │───►│ Disconnected│
│  (WebSocket)│    │  (Service)  │    │  (Cleanup)  │
└─────────────┘    └─────────────┘    └─────────────┘
```

1. **Connected**: WebSocket handshake complete, authenticated
2. **Joined**: User joined a service, presence broadcast
3. **Disconnected**: Connection closed, presence updated

## Validation Summary

| Entity | Required Fields | Max Size | Unique Constraints |
|--------|----------------|----------|-------------------|
| User | userId, displayName | N/A | userId |
| Conversation | serviceId, participantIds | 500 participants | N/A |
| Message | conversationId, senderId, type | 10KB text, 100MB file | N/A |
| Connection | userId, connectionId, podId | N/A | connectionId |
| FileMetadata | blobId, fileName, mimeType | 100 MB | blobId |

## Query Patterns

1. **Conversation messages**: `db.messages.find({ conversationId: "..." }).sort({ createdAt: -1 }).limit(50)`
2. **User conversations**: `db.conversations.find({ participantIds: "userId" }).sort({ lastMessageAt: -1 })`
3. **Recent connections**: `db.connections.find({ userId: "..." }).sort({ connectedAt: -1 }).limit(10)`
4. **Service activity**: `db.messages.find({ serviceId: "...", createdAt: { $gte: start } })`
