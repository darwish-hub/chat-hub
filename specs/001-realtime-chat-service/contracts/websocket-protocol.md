# WebSocket Wire Protocol

**Version**: 1.0  
**Date**: 2026-05-09  
**Transport**: Native WebSocket (RFC 6455)

## Connection

### Endpoint

```
ws://host/ws?token={jwt}
```

**Query Parameters**:
- `token` (required): JWT authentication token

**Headers**:
- Standard WebSocket upgrade headers
- No custom headers required

### Authentication

Token must be valid and not expired. Server validates before accepting WebSocket.

**Success**: HTTP 101 Switching Protocols  
**Failure**: HTTP 401 Unauthorized (before upgrade)

## Message Format

All text messages use JSON with camelCase property names.

### Base Envelope

```json
{
  "type": "string",
  ...type-specific fields
}
```

## Client → Server Messages

### join_service

Subscribe to a service to receive messages and presence updates.

```json
{
  "type": "join_service",
  "serviceId": "string"
}
```

**Fields**:
- `serviceId` (required): Service identifier to join

---

### leave_service

Unsubscribe from a service.

```json
{
  "type": "leave_service",
  "serviceId": "string"
}
```

**Fields**:
- `serviceId` (required): Service identifier to leave

---

### text_message

Send a text message to a conversation.

```json
{
  "type": "text_message",
  "id": "uuid",
  "conversationId": "string",
  "serviceId": "string",
  "text": "string",
  "replyToId": "string | null"
}
```

**Fields**:
- `id` (required): Client-generated UUID for message
- `conversationId` (required): Target conversation
- `serviceId` (required): Service context
- `text` (required): Message content, max 10,000 chars
- `replyToId` (optional): Message being replied to

**Validation Errors**:
- `invalid_message`: Text empty or exceeds limit
- `not_participant`: User not in conversation
- `rate_limit_exceeded`: Too many messages sent

---

### voice_chunk

Start a live voice stream. Followed immediately by binary frames.

```json
{
  "type": "voice_chunk",
  "id": "uuid",
  "conversationId": "string",
  "sequenceNumber": 0,
  "isFinal": false
}
```

**Fields**:
- `id` (required): Voice message UUID
- `conversationId` (required): Target conversation
- `sequenceNumber` (required): Monotonically increasing (0, 1, 2...)
- `isFinal` (required): `false` for chunks, `true` for final marker

**Binary Frame**: Immediately after JSON, send binary frame with audio payload.

---

### voice_message

Send a pre-recorded voice message (already uploaded).

```json
{
  "type": "voice_message",
  "id": "uuid",
  "conversationId": "string",
  "blobId": "string",
  "durationMs": 5000,
  "mimeType": "audio/opus"
}
```

**Fields**:
- `id` (required): Message UUID
- `conversationId` (required): Target conversation
- `blobId` (required): Reference to uploaded voice file
- `durationMs` (required): Audio length in milliseconds
- `mimeType` (required): Audio format

---

### file_attachment

Share a file in a conversation (already uploaded).

```json
{
  "type": "file_attachment",
  "id": "uuid",
  "conversationId": "string",
  "blobId": "string",
  "fileName": "document.pdf",
  "mimeType": "application/pdf",
  "sizeBytes": 1024000
}
```

**Fields**:
- `id` (required): Message UUID
- `conversationId` (required): Target conversation
- `blobId` (required): Reference to uploaded file
- `fileName` (required): Original filename
- `mimeType` (required): File MIME type
- `sizeBytes` (required): File size in bytes

---

### typing

Indicate typing activity.

```json
{
  "type": "typing",
  "conversationId": "string",
  "isTyping": true
}
```

**Fields**:
- `conversationId` (required): Target conversation
- `isTyping` (required): `true` when typing starts, `false` when stopped

---

### ack

Acknowledge message delivery.

```json
{
  "type": "ack",
  "messageId": "string"
}
```

**Fields**:
- `messageId` (required): ID of message being acknowledged

---

### pong

Response to server ping.

```json
{
  "type": "pong"
}
```

**Timing**: Must respond within 10 seconds of receiving ping.

## Server → Client Messages

### message_received

New message in a conversation.

```json
{
  "type": "message_received",
  "envelope": {
    "id": "uuid",
    "conversationId": "string",
    "serviceId": "string",
    "senderId": "string",
    "type": "text | voice | file",
    "text": "string | null",
    "voice": {
      "blobId": "string",
      "durationMs": 5000,
      "mimeType": "audio/opus"
    } | null,
    "file": {
      "blobId": "string",
      "fileName": "string",
      "mimeType": "string",
      "sizeBytes": 1024000
    } | null,
    "replyToId": "string | null",
    "createdAt": "2024-01-15T10:30:00Z"
  }
}
```

---

### voice_chunk

Live voice stream chunk from another user.

```json
{
  "type": "voice_chunk",
  "id": "uuid",
  "conversationId": "string",
  "sequenceNumber": 0,
  "isFinal": false,
  "fromUserId": "string"
}
```

**Binary Frame**: Immediately after JSON, binary frame with audio payload follows.

---

### user_joined

User joined a service.

```json
{
  "type": "user_joined",
  "userId": "string",
  "serviceId": "string",
  "displayName": "string"
}
```

---

### user_left

User left a service.

```json
{
  "type": "user_left",
  "userId": "string",
  "serviceId": "string"
}
```

---

### typing

Typing indicator from another user.

```json
{
  "type": "typing",
  "userId": "string",
  "conversationId": "string",
  "isTyping": true
}
```

---

### delivered

Message delivery confirmation.

```json
{
  "type": "delivered",
  "messageId": "string"
}
```

---

### error

Error response.

```json
{
  "type": "error",
  "code": "rate_limit_exceeded | invalid_message | not_participant | server_error",
  "message": "string",
  "correlationId": "string"
}
```

**Error Codes**:
- `rate_limit_exceeded`: Too many messages
- `invalid_message`: Malformed or invalid message
- `not_participant`: User not in conversation
- `server_error`: Internal server error

---

### ping

Heartbeat from server.

```json
{
  "type": "ping"
}
```

**Frequency**: Every 15 seconds. Client must respond with `pong` within 10 seconds.

## Error Handling

### Connection Errors

- **Unexpected close**: Client should reconnect and fetch missed messages
- **Authentication failure**: Refresh token and reconnect
- **Rate limit**: Back off and retry with exponential delay

### Message Errors

Errors are sent as `error` messages with correlation ID matching the original message ID when applicable.

## Rate Limits

- **Text/file messages**: 100 per connection per minute
- **Voice messages**: 10 per connection per minute

Exceeding limits returns `error` with code `rate_limit_exceeded`.

## Binary Protocol

Binary frames are used only for:
1. **Voice chunks**: Audio payload sent after `voice_chunk` JSON envelope

**Format**:
- No additional framing
- Raw audio bytes (Opus codec recommended)
- Reassemble by `sequenceNumber` on receive
