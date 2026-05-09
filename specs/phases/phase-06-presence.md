# Phase 6: Presence and Typing Indicators

**Priority**: P2 (Enhancement)  
**Status**: Ready for Implementation  
**Dependencies**: Phase 1 (Setup), Phase 2 (Foundational), Phase 3 (Text Messaging)

## Overview

Enable users to see which users are currently online and available, and receive typing indicators when someone is composing a message. This manages user expectations and encourages real-time engagement.

## User Story

**As a** chat user,  
**I want** to see which users are currently online and available,  
**So that** I know when to expect responses.

## Acceptance Criteria

1. **Given** a user connects to the service, **When** they join a service or conversation, **Then** other participants see that they are online
2. **Given** a user disconnects or goes offline, **When** they leave or lose connection, **Then** other participants see their status change to offline
3. **Given** users are viewing a conversation, **When** someone starts typing, **Then** other participants see a typing indicator

## Functional Requirements

- **FR-P001**: User online status MUST be tracked per service
- **FR-P002**: Presence MUST be stored in Redis with 2-minute expiry
- **FR-P003**: User join/leave events MUST be broadcast to service participants
- **FR-P004**: Typing indicators MUST be sent when a user starts/stops typing
- **FR-P005**: Typing indicators MUST include debouncing (e.g., stop after 3 seconds of inactivity)
- **FR-P006**: Presence status MUST be queryable via API
- **FR-P007**: Status updates MUST propagate across pods via NATS

## Success Criteria

- **SC-P001**: Presence status updates are visible to other users within 2 seconds
- **SC-P002**: Online users list is accurate (false positives < 1%)
- **SC-P003**: Typing indicators appear within 500ms of user typing
- **SC-P004**: Typing indicators clear within 5 seconds of inactivity
- **SC-P005**: Presence data is memory-efficient (uses Redis TTL)

## Technical Implementation

### Data Flow

```
User Joins:
Client → join_service → Handler → Redis (presence) → NATS → Broadcast

User Leaves:
Client → leave_service/disconnect → Handler → Redis → NATS → Broadcast

Typing:
Client → typing (isTyping: true) → Handler → Debounce → NATS → Broadcast
      → typing (isTyping: false) after timeout
```

### Components

1. **RedisPresenceService** - Already implemented in Phase 2
2. **JoinServiceHandler** - Already implemented in Phase 3, needs presence update
3. **TypingHandler** - New handler for typing indicators
4. **PresenceController** - API endpoint for querying online users

### Files to Create/Modify

- `ChatHub.Api/Handlers/TypingHandler.cs`
- `ChatHub.Api/Controllers/PresenceController.cs`

## Wire Protocol

### Client → Server: Typing

```json
{
  "type": "typing",
  "conversationId": "conv-123",
  "isTyping": true
}
```

### Server → Client: User Joined

```json
{
  "type": "user_joined",
  "userId": "user-123",
  "serviceId": "service-123",
  "displayName": "John Doe"
}
```

### Server → Client: User Left

```json
{
  "type": "user_left",
  "userId": "user-123",
  "serviceId": "service-123"
}
```

### Server → Client: Typing Indicator

```json
{
  "type": "typing",
  "userId": "user-123",
  "conversationId": "conv-123",
  "isTyping": true
}
```

## REST API Endpoint

### Get Online Users

```
GET /api/services/{serviceId}/online
Authorization: Bearer {token}
```

**Response:**
```json
{
  "serviceId": "service-123",
  "onlineUsers": [
    {
      "userId": "user-123",
      "displayName": "John Doe",
      "lastSeen": "2024-01-15T10:30:00Z"
    }
  ]
}
```

## Implementation Tasks

- [X] T060 Create presence tracking in RedisPresenceService
- [X] T061 Implement UserJoined broadcasting on service join
- [X] T062 Implement UserLeft broadcasting on service leave/disconnect
- [X] T063 Create TypingHandler with debouncing
- [X] T064 Implement typing indicator broadcasting
- [X] T065 Add presence status to connection registry
- [X] T066 Create endpoint to fetch online users

## Definition of Done

- [ ] Users see online status of other participants
- [ ] Join/leave events are broadcast in real-time
- [ ] Typing indicators appear/disappear correctly
- [ ] Presence data is accurate across pod restarts
- [ ] API endpoint returns current online users
- [ ] Integration tests verify presence flow
