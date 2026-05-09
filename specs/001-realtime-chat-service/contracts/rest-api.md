# REST API

**Version**: 1.0  
**Date**: 2026-05-09  
**Base URL**: `https://api.chathub.example.com`

## Authentication

All endpoints require Bearer token authentication.

```
Authorization: Bearer {jwt}
```

## Endpoints

### Upload Voice File

Upload a pre-recorded voice file for sharing via WebSocket.

```
POST /api/upload/voice
```

**Request**:
- Content-Type: `multipart/form-data`
- Max size: 25 MB

**Form Fields**:
- `file` (required): Audio file (Opus, MP3, WAV)

**Response** (200 OK):
```json
{
  "blobId": "uuid",
  "fileName": "voice.opus",
  "mimeType": "audio/opus",
  "sizeBytes": 50000
}
```

**Response Fields**:
- `blobId`: Reference to use in `voice_message` WebSocket message
- `fileName`: Sanitized filename
- `mimeType`: Detected MIME type
- `sizeBytes`: File size

**Errors**:
- `400 Bad Request`: File too large or invalid format
- `401 Unauthorized`: Missing or invalid token
- `413 Payload Too Large`: File exceeds 25 MB limit

---

### Upload File Attachment

Upload a file for sharing via WebSocket.

```
POST /api/upload/file
```

**Request**:
- Content-Type: `multipart/form-data`
- Max size: 100 MB

**Form Fields**:
- `file` (required): Any file type

**Response** (200 OK):
```json
{
  "blobId": "uuid",
  "fileName": "document.pdf",
  "mimeType": "application/pdf",
  "sizeBytes": 1024000,
  "url": "https://storage.example.com/..."
}
```

**Response Fields**:
- `blobId`: Reference to use in `file_attachment` WebSocket message
- `fileName`: Sanitized filename
- `mimeType`: Detected MIME type
- `sizeBytes`: File size
- `url`: Pre-signed download URL (optional, may be null)

**Errors**:
- `400 Bad Request`: Invalid file or missing field
- `401 Unauthorized`: Missing or invalid token
- `413 Payload Too Large`: File exceeds 100 MB limit
- `415 Unsupported Media Type`: File type not allowed

---

### Download File

Download a previously uploaded file.

```
GET /api/download/{blobId}
```

**Path Parameters**:
- `blobId` (required): File identifier from upload

**Response** (200 OK):
- Content-Type: File's MIME type
- Body: File bytes

**Errors**:
- `401 Unauthorized`: Missing or invalid token
- `404 Not Found`: File not found
- `410 Gone`: File expired or deleted

---

### Health Check

Check API liveness.

```
GET /health
```

**Response** (200 OK):
```json
{
  "status": "healthy",
  "timestamp": "2024-01-15T10:30:00Z"
}
```

---

### Readiness Check

Check if service is ready to accept traffic.

```
GET /health/ready
```

**Response** (200 OK):
```json
{
  "status": "ready",
  "checks": {
    "mongodb": "healthy",
    "redis": "healthy",
    "nats": "healthy"
  }
}
```

**Response** (503 Service Unavailable):
```json
{
  "status": "not_ready",
  "checks": {
    "mongodb": "unhealthy",
    "redis": "healthy",
    "nats": "healthy"
  }
}
```

---

### Get Conversation History

Fetch message history for a conversation.

```
GET /api/conversations/{conversationId}/messages?before={timestamp}&limit={number}
```

**Path Parameters**:
- `conversationId` (required): Conversation identifier

**Query Parameters**:
- `before` (optional): ISO timestamp, fetch messages before this time
- `limit` (optional): Number of messages to fetch (default: 50, max: 100)

**Response** (200 OK):
```json
{
  "conversationId": "string",
  "messages": [
    {
      "id": "uuid",
      "senderId": "string",
      "type": "text | voice | file",
      "text": "string | null",
      "voice": { "blobId": "string", "durationMs": 5000, "mimeType": "string" } | null,
      "file": { "blobId": "string", "fileName": "string", "mimeType": "string", "sizeBytes": 0 } | null,
      "replyToId": "string | null",
      "createdAt": "2024-01-15T10:30:00Z"
    }
  ],
  "hasMore": true
}
```

**Errors**:
- `401 Unauthorized`: Missing or invalid token
- `403 Forbidden`: User not in conversation
- `404 Not Found`: Conversation not found

---

### Create Conversation

Create a new conversation.

```
POST /api/conversations
```

**Request Body**:
```json
{
  "serviceId": "string",
  "title": "string | null",
  "participantIds": ["string"]
}
```

**Request Fields**:
- `serviceId` (required): Service to create conversation in
- `title` (optional): Conversation name
- `participantIds` (required): Array of user IDs (min 1 other participant)

**Response** (201 Created):
```json
{
  "id": "uuid",
  "serviceId": "string",
  "title": "string | null",
  "participantIds": ["string"],
  "createdAt": "2024-01-15T10:30:00Z"
}
```

**Errors**:
- `400 Bad Request`: Invalid participants or missing fields
- `401 Unauthorized`: Missing or invalid token
- `403 Forbidden`: Not authorized to create in service

---

### Join Service

Join a service (creates presence entry).

```
POST /api/services/{serviceId}/join
```

**Response** (200 OK):
```json
{
  "serviceId": "string",
  "joinedAt": "2024-01-15T10:30:00Z"
}
```

**Errors**:
- `401 Unauthorized`: Missing or invalid token
- `404 Not Found`: Service not found

---

### Leave Service

Leave a service.

```
POST /api/services/{serviceId}/leave
```

**Response** (200 OK):
```json
{
  "serviceId": "string",
  "leftAt": "2024-01-15T10:30:00Z"
}
```

## Error Format

All errors follow this format:

```json
{
  "error": {
    "code": "string",
    "message": "string",
    "details": { }
  }
}
```

**Common Error Codes**:
- `invalid_request`: Malformed request
- `unauthorized`: Authentication required
- `forbidden`: Permission denied
- `not_found`: Resource not found
- `rate_limited`: Too many requests
- `internal_error`: Server error

## Rate Limiting

API endpoints have rate limits separate from WebSocket:

- **Upload endpoints**: 10 requests per minute per user
- **History endpoints**: 60 requests per minute per user
- **Other endpoints**: 120 requests per minute per user

Rate limit headers included in responses:
```
X-RateLimit-Limit: 60
X-RateLimit-Remaining: 59
X-RateLimit-Reset: 1705315800
```
