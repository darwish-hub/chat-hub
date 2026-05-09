# Phase 7: Message Replies

**Priority**: P3 (Nice to Have)  
**Status**: Ready for Implementation  
**Dependencies**: Phase 1 (Setup), Phase 2 (Foundational), Phase 3 (Text Messaging)

## Overview

Enable users to reply to specific messages, maintaining context and allowing navigation to referenced messages. This improves conversation organization in busy chat rooms.

## User Story

**As a** chat user,  
**I want** to reply to specific messages,  
**So that** I can maintain context in busy conversations.

## Acceptance Criteria

1. **Given** a conversation has existing messages, **When** a user selects a message to reply, **Then** they can compose a response linked to that specific message
2. **Given** a reply is sent, **When** other participants view the conversation, **Then** they see the reply connected to the original message with proper context
3. **Given** a user views a reply, **When** they click on it, **Then** they can navigate to the original message being referenced

## Functional Requirements

- **FR-R001**: Users MUST be able to include a replyToId when sending any message type
- **FR-R002**: replyToId MUST reference an existing message in the same conversation
- **FR-R003**: Replies MUST be visible in message history with context
- **FR-R004**: Thread view MUST be available to see all replies to a message
- **FR-R005**: Reply metadata MUST be stored in MongoDB
- **FR-R006**: Reply context MUST be included in message broadcasts

## Success Criteria

- **SC-R001**: Replies are linked to original messages 100% of the time
- **SC-R002**: Reply context is visible within 1 second of sending
- **SC-R003**: Thread view loads within 2 seconds
- **SC-R004**: Invalid replyToId returns clear error message
- **SC-R005**: Replies work for text, voice, and file messages

## Technical Implementation

### Data Flow

```
Client sends reply:
Client → WebSocket message (with replyToId) → Handler → Validation
                                                    ↓
                                             MongoDB (persist)
                                                    ↓
                                             NATS (broadcast)
                                                    ↓
                                        All clients receive with context
```

### Components

1. **TextMessageHandler** - Already implemented, needs reply support
2. **VoiceMessageHandler** - Needs reply support
3. **FileAttachmentHandler** - Needs reply support
4. **ConversationController** - Add thread view endpoint

### Files to Modify

- `ChatHub.Api/Handlers/TextMessageHandler.cs`
- `ChatHub.Api/Handlers/VoiceMessageHandler.cs`
- `ChatHub.Api/Handlers/FileAttachmentHandler.cs`
- `ChatHub.Api/Controllers/ConversationController.cs`

## Wire Protocol

### Client → Server: Reply to Message

Any message type can include a replyToId:

```json
{
  "type": "text_message",
  "id": "msg-uuid",
  "conversationId": "conv-123",
  "serviceId": "service-123",
  "text": "This is a reply",
  "replyToId": "original-msg-uuid"
}
```

### Server → Client: Message with Reply Context

```json
{
  "type": "message_received",
  "envelope": {
    "id": "msg-uuid",
    "type": "text",
    "text": "This is a reply",
    "replyToId": "original-msg-uuid",
    "createdAt": "2024-01-15T10:30:00Z"
  }
}
```

## REST API Endpoint

### Get Message Thread

```
GET /api/conversations/{conversationId}/messages/{messageId}/replies
Authorization: Bearer {token}
```

**Response:**
```json
{
  "originalMessage": {
    "id": "original-msg-uuid",
    "senderId": "user-123",
    "type": "text",
    "text": "Original message"
  },
  "replies": [
    {
      "id": "reply-msg-uuid",
      "senderId": "user-456",
      "type": "text",
      "text": "This is a reply"
    }
  ]
}
```

## Implementation Tasks

- [X] T067 Update TextMessageHandler to support replyToId
- [X] T068 Update VoiceMessageHandler to support replyToId
- [X] T069 Update FileAttachmentHandler to support replyToId
- [X] T070 Add message threading query endpoint
- [X] T071 Update MessageReceived to include reply context

## Definition of Done

- [ ] Users can reply to text messages
- [ ] Users can reply to voice messages
- [ ] Users can reply to file messages
- [ ] Replies are visually linked in client
- [ ] Thread view shows all replies
- [ ] Invalid replyToId is rejected with error
- [ ] Integration tests verify reply functionality
