# Phase 5: File Sharing

**Priority**: P2 (Enhancement)  
**Status**: Ready for Implementation  
**Dependencies**: Phase 1 (Setup), Phase 2 (Foundational), Phase 3 (Text Messaging)

## Overview

Enable users to upload and share attachments (documents, images, voice, and video) in conversations, with other participants able to view metadata and download files. This enhances collaboration beyond text messages. Voice and video are treated as attachments with the same upload/share flow, differentiated by MIME type.

## User Story

**As a** chat user,  
**I want** to share files (documents, images, videos) in conversations,  
**So that** I can exchange rich content with other participants.

## Acceptance Criteria

1. **Given** a user wants to share a file, **When** they upload it through the service, **Then** they receive a reference to share in the conversation
2. **Given** a file is shared in a conversation, **When** other participants view the conversation, **Then** they see the file metadata (name, size, type) and can download it
3. **Given** a shared file is available, **When** a participant clicks to download, **Then** they can retrieve the complete file content

## Functional Requirements

- **FR-F001**: Users MUST be able to upload attachments (files, voice, video) via REST API endpoint
- **FR-F002**: Attachment uploads MUST support streaming to S3 without memory buffering
- **FR-F003**: Attachments up to 100 MB MUST be supported
- **FR-F004**: Attachment metadata (name, size, type, blobId, optional duration) MUST be returned after upload
- **FR-F005**: Users MUST be able to share uploaded attachments via WebSocket `file_attachment` message
- **FR-F006**: Attachment metadata MUST be persisted to MongoDB when shared
- **FR-F007**: Participants MUST be able to download attachments via authenticated URL
- **FR-F008**: Pre-signed URLs MUST be generated for secure downloads
- **FR-F009**: Rate limiting MUST apply (100 attachment operations per minute per connection)
- **FR-F010**: The server MUST infer the message type (`voice`, `video`, or `file`) from the attachment MIME type

## Success Criteria

- **SC-F001**: Users can upload files up to 100 MB in size
- **SC-F002**: File downloads complete at a minimum speed of 1 MB/s for files under 50 MB
- **SC-F003**: Upload streaming prevents memory issues with large files
- **SC-F004**: File metadata is visible within 1 second of sharing
- **SC-F005**: Download URLs are valid for 24 hours

## Technical Implementation

### Data Flow

```
Upload:
Client → POST /api/upload/file → Stream to S3 → Return blobId + metadata

Share:
Client → WebSocket file_attachment → Handler → MongoDB (type inferred from MIME) → NATS broadcast

Download:
Client → GET /api/download/{blobId} → Pre-signed URL → S3 → File
```

### Components

1. **UploadController** - Handles multipart file uploads with streaming
2. **FileAttachmentHandler** - Processes file share messages
3. **S3BlobStorageClient** - Already implemented in Phase 2

### Files to Create/Modify

- `ChatHub.Api/Controllers/UploadController.cs`
- `ChatHub.Api/Handlers/FileAttachmentHandler.cs`
- `ChatHub.Api/Handlers/VoiceMessageHandler.cs` (for live streaming completion)

## REST API Endpoints

### Upload File

```
POST /api/upload/file
Content-Type: multipart/form-data
Authorization: Bearer {token}

file: <binary data>
```

**Response:**
```json
{
  "blobId": "uuid",
  "fileName": "document.pdf",
  "mimeType": "application/pdf",
  "sizeBytes": 1024000
}
```

### Download File

```
GET /api/download/{blobId}
Authorization: Bearer {token}
```

**Response:** File bytes or 302 redirect to pre-signed URL

## Wire Protocol

### Client → Server: File Attachment

```json
{
  "type": "file_attachment",
  "id": "msg-uuid",
  "conversationId": "conv-123",
  "blobId": "blob-uuid-from-upload",
  "fileName": "document.pdf",
  "mimeType": "application/pdf",
  "sizeBytes": 1024000
}
```

### Server → Client: File Received

```json
{
  "type": "message_received",
  "envelope": {
    "id": "msg-uuid",
    "type": "file",
    "file": {
      "blobId": "blob-uuid",
      "fileName": "document.pdf",
      "mimeType": "application/pdf",
      "sizeBytes": 1024000
    }
  }
}
```

## Implementation Tasks

- [X] T053 Extend UploadController with file upload endpoint
- [X] T054 Implement file validation (size, type)
- [X] T055 Implement direct S3 streaming upload
- [X] T056 Create FileAttachmentHandler
- [X] T057 Implement file metadata persistence
- [X] T058 Create file download endpoint
- [X] T059 Implement pre-signed URL generation

## Definition of Done

- [ ] Users can upload files up to 100 MB
- [ ] Files are streamed directly to S3
- [ ] File metadata is returned and can be shared
- [ ] Participants can download shared files
- [ ] Rate limiting prevents abuse
- [ ] Integration tests verify upload/download flow
