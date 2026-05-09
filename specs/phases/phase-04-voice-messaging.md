# Phase 4: Live Voice Messaging

**Priority**: P1 (Core Feature)  
**Status**: Ready for Implementation  
**Dependencies**: Phase 1 (Setup), Phase 2 (Foundational), Phase 3 (Text Messaging)

## Overview

Enable users to send voice messages in real-time, allowing other participants to hear audio as it's being recorded (streaming), not just after the recording completes. This provides a walkie-talkie-like experience for hands-free communication.

## User Story

**As a** chat user,  
**I want** to send voice messages in real-time,  
**So that** I can communicate without typing, similar to a walkie-talkie experience.

## Acceptance Criteria

1. **Given** a user is in a conversation, **When** they start recording a voice message, **Then** other participants receive audio chunks in real-time as they are recorded
2. **Given** a voice message is being streamed, **When** the sender stops recording, **Then** the complete voice message is saved and available for replay
3. **Given** a user is receiving a live voice stream, **When** audio chunks arrive, **Then** they are played in the correct sequence without gaps

## Functional Requirements

- **FR-V001**: Users MUST be able to start a voice recording session with a unique message ID
- **FR-V002**: Voice chunks MUST be streamed in real-time to other conversation participants
- **FR-V003**: Voice chunks MUST be temporarily stored in Redis with sequence ordering
- **FR-V004**: On recording completion, chunks MUST be assembled and uploaded to S3 storage
- **FR-V005**: Voice metadata (duration, blob reference) MUST be persisted to MongoDB
- **FR-V006**: Rate limiting MUST apply (10 voice messages per minute per connection)
- **FR-V007**: Voice chunks MUST use binary WebSocket frames for efficiency
- **FR-V008**: Participants MUST receive voice chunks in correct sequence order

## Success Criteria

- **SC-V001**: Voice messages stream with less than 500ms latency between sender and receiver
- **SC-V002**: Users can send up to 10 voice messages per minute without rate limiting
- **SC-V003**: Voice chunk assembly succeeds 99.9% of the time
- **SC-V004**: Audio playback has no gaps or sequence errors
- **SC-V005**: Voice messages up to 5 minutes (300 seconds) are supported

## Technical Implementation

### Data Flow

```
Client (recording) → WebSocket → VoiceChunkHandler → Redis (sorted set)
                                                        ↓
Client (stop) → WebSocket → VoiceMessageHandler → Retrieve chunks → S3
                                                        ↓
                                               MongoDB (metadata)
                                                        ↓
                                               NATS (broadcast)
```

### Components

1. **VoiceChunkHandler** - Receives and forwards chunks, stores in Redis
2. **VoiceMessageHandler** - Handles recording completion, assembles and uploads
3. **VoiceSessionBuffer** - Redis-based chunk storage with sequence tracking
4. **Binary Frame Handler** - WebSocket binary message processing

### Files to Create/Modify

- `ChatHub.Api/Handlers/VoiceChunkHandler.cs`
- `ChatHub.Api/Handlers/VoiceMessageHandler.cs`
- `ChatHub.Infrastructure/Cache/VoiceSessionBuffer.cs`
- `ChatHub.Api/Middleware/WebSocketMiddleware.cs` (binary frame support)

## Wire Protocol

### Client → Server: Voice Chunk

```json
{
  "type": "voice_chunk",
  "id": "msg-uuid",
  "conversationId": "conv-123",
  "sequenceNumber": 0,
  "isFinal": false
}
```
Followed immediately by a binary frame containing the audio payload.

### Client → Server: Voice Message Complete

```json
{
  "type": "voice_message",
  "id": "msg-uuid",
  "conversationId": "conv-123",
  "blobId": "uploaded-blob-id",
  "durationMs": 5000,
  "mimeType": "audio/opus"
}
```

### Server → Client: Voice Chunk

```json
{
  "type": "voice_chunk",
  "id": "msg-uuid",
  "conversationId": "conv-123",
  "sequenceNumber": 0,
  "isFinal": false,
  "fromUserId": "user-123"
}
```
Followed by binary frame with audio payload.

## Implementation Tasks

- [X] T042 Create VoiceSessionBuffer for Redis chunk storage
- [X] T043 Implement voice chunk storage in Redis sorted sets
- [X] T044 Create VoiceChunkHandler for live streaming
- [X] T045 Implement binary frame parsing in receive loop
- [X] T046 Implement immediate forwarding of voice chunks
- [X] T047 Create VoiceMessageHandler for completed recordings
- [X] T048 Implement voice assembly from Redis chunks
- [X] T049 Implement S3 upload for assembled voice
- [X] T050 Implement MongoDB persistence for voice metadata
- [X] T051 Create UploadController for pre-recorded voice uploads
- [X] T052 Add voice file validation

## Definition of Done

- [X] Users can record and stream voice messages in real-time
- [X] Audio chunks arrive in sequence without gaps
- [X] Completed voice messages are stored and replayable
- [X] Rate limiting prevents abuse
- [ ] Integration tests verify end-to-end flow
