# Tasks: Real-time Chat Service

**Input**: Design documents from `/specs/001-realtime-chat-service/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), data-model.md, contracts/, research.md, quickstart.md

**Tests**: Tests are optional - only include if explicitly requested. For this implementation, we'll include integration tests for critical paths.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- `ChatHub.Core/` - Models, DTOs, interfaces, settings
- `ChatHub.Infrastructure/` - WebSockets, NATS, MongoDB, Redis, S3 implementations
- `ChatHub.Api/` - Middleware, controllers, health checks
- `ChatHub.Tests/` - Unit and integration tests
- `k8s/` - Kubernetes manifests

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [X] T001 Create solution structure with ChatHub.Core, ChatHub.Infrastructure, ChatHub.Api, ChatHub.Tests projects
- [X] T002 [P] Initialize .NET 8 solution file and project references
- [X] T003 [P] Add NuGet packages: MongoDB.Driver, NATS.Client, StackExchange.Redis, AWSSDK.S3
- [X] T004 [P] Add test packages: xUnit, Testcontainers.MongoDb, Testcontainers.Redis, Microsoft.AspNetCore.TestHost
- [X] T005 Create docker-compose.yml with MongoDB, Redis, NATS, MinIO services
- [X] T006 Create .env.example with configuration template

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T007 [P] Create ChatHub.Core settings classes: ChatHubSettings.cs, MongoSettings.cs, NatsSettings.cs, RedisSettings.cs, StorageSettings.cs
- [X] T008 [P] Create ChatHub.Core interfaces: IConnectionRegistry.cs, IWebSocketSender.cs, INatsBackplane.cs, IMessageDispatcher.cs
- [X] T009 [P] Create ChatHub.Core interfaces: IMessageRepository.cs, IConversationRepository.cs, IBlobStorageClient.cs, IRateLimiter.cs
- [X] T010 Create WebSocketConnection record in ChatHub.Infrastructure/WebSockets/WebSocketConnection.cs
- [X] T011 Create ConnectionRegistry implementation in ChatHub.Infrastructure/WebSockets/ConnectionRegistry.cs
- [X] T012 Create WebSocketSender implementation in ChatHub.Infrastructure/WebSockets/WebSocketSender.cs
- [X] T013 Create NatsBackplane implementation in ChatHub.Infrastructure/Nats/NatsBackplane.cs
- [X] T014 Create NatsSubscriberService BackgroundService in ChatHub.Infrastructure/Nats/NatsSubscriberService.cs
- [X] T015 Create MongoInitializer IHostedService in ChatHub.Infrastructure/Persistence/MongoInitializer.cs
- [X] T016 Create MongoWriterService BackgroundService with Channel in ChatHub.Infrastructure/Writers/MongoWriterService.cs
- [X] T017 Create base ClientMessage and ServerMessage DTOs in ChatHub.Core/Models/
- [X] T018 Create MessageDocument, ConversationDocument, ConnectionDocument in ChatHub.Core/Documents/
- [X] T019 Implement MessageRepository in ChatHub.Infrastructure/Persistence/MessageRepository.cs
- [X] T020 Implement ConversationRepository in ChatHub.Infrastructure/Persistence/ConversationRepository.cs
- [X] T021 Implement S3BlobStorageClient in ChatHub.Infrastructure/Storage/S3BlobStorageClient.cs
- [X] T022 Implement RedisRateLimiter in ChatHub.Infrastructure/Cache/RedisRateLimiter.cs
- [X] T023 Implement RedisPresenceService in ChatHub.Infrastructure/Cache/RedisPresenceService.cs
- [X] T024 Create WebSocketMiddleware with handshake and connection management in ChatHub.Api/Middleware/WebSocketMiddleware.cs
- [X] T025 Implement receive loop with frame handling in WebSocketMiddleware.cs
- [X] T026 Implement heartbeat loop in WebSocketMiddleware.cs
- [X] T027 Create IMessageDispatcher and IMessageHandler<T> pattern in ChatHub.Core/Interfaces/
- [X] T028 Create MessageDispatcher implementation in ChatHub.Api/
- [X] T029 Create health checks: MongoHealthCheck.cs, RedisHealthCheck.cs, NatsHealthCheck.cs in ChatHub.Api/HealthChecks/
- [X] T030 Wire up all services in ChatHub.Api/Program.cs with DI container

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Send and Receive Real-time Text Messages (Priority: P1) 🎯 MVP

**Goal**: Enable users to connect via WebSocket, send text messages to conversations, and receive messages from others in real-time with delivery confirmation.

**Independent Test**: A user can connect to the service at ws://localhost:8080/ws, join a service, send a text message, and receive delivery confirmation within 1 second. Another connected user in the same conversation receives the message.

### Implementation for User Story 1

- [X] T031 [P] [US1] Create JoinService message handler in ChatHub.Api/Handlers/JoinServiceHandler.cs
- [X] T032 [P] [US1] Create LeaveService message handler in ChatHub.Api/Handlers/LeaveServiceHandler.cs
- [X] T033 [US1] Create TextMessage message handler in ChatHub.Api/Handlers/TextMessageHandler.cs
- [X] T034 [US1] Implement message persistence logic in TextMessageHandler (write to MongoDB via channel)
- [X] T035 [US1] Implement NATS publish after successful MongoDB write in TextMessageHandler
- [X] T036 [US1] Implement local delivery to connections on same pod in TextMessageHandler
- [X] T037 [US1] Create Delivered message handler for acknowledgments in ChatHub.Api/Handlers/DeliveredHandler.cs
- [X] T038 [US1] Implement Pong handler for heartbeat responses in ChatHub.Api/Handlers/PongHandler.cs
- [X] T039 [US1] Create conversation management endpoint in ChatHub.Api/Controllers/ConversationController.cs
- [X] T040 [US1] Add conversation history retrieval endpoint in ConversationController.cs
- [X] T041 [US1] Add WebSocket protocol error handling and error message generation

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently. Users can connect, join services, send/receive text messages, and get delivery confirmations.

---

## Phase 4: User Story 2 - Live Voice Messaging (Priority: P1)

**Goal**: Enable users to record and stream voice messages in real-time, allowing other participants to hear audio as it's being recorded, with final messages stored for replay.

**Independent Test**: A user can start recording a voice message, and other participants in the conversation receive audio chunks in real-time. When recording stops, the complete voice message is available for replay.

### Implementation for User Story 2

- [X] T042 [P] [US2] Create VoiceSessionBuffer for chunk assembly in ChatHub.Infrastructure/Cache/VoiceSessionBuffer.cs
- [X] T043 [US2] Implement voice chunk storage in Redis sorted sets in VoiceSessionBuffer.cs
- [X] T044 [US2] Create VoiceChunk message handler for live streaming in ChatHub.Api/Handlers/VoiceChunkHandler.cs
- [X] T045 [US2] Implement binary frame parsing for audio chunks in receive loop
- [X] T046 [US2] Implement immediate forwarding of voice chunks to other participants in VoiceChunkHandler
- [X] T047 [US2] Create VoiceMessage message handler for completed recordings in ChatHub.Api/Handlers/VoiceMessageHandler.cs
- [X] T048 [US2] Implement voice assembly from Redis chunks in VoiceMessageHandler
- [X] T049 [US2] Implement S3 upload for assembled voice in VoiceMessageHandler
- [X] T050 [US2] Implement MongoDB persistence for voice message metadata in VoiceMessageHandler
- [X] T051 [US2] Create UploadController for pre-recorded voice uploads in ChatHub.Api/Controllers/UploadController.cs
- [X] T052 [US2] Add voice file validation and processing in UploadController.cs

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently. Users can send text and voice messages.

---

## Phase 5: User Story 3 - Share Files in Conversations (Priority: P2)

**Goal**: Enable users to upload files (documents, images, videos) and share them in conversations, with other participants able to view metadata and download files.

**Independent Test**: A user can upload a file via REST API, receive a blobId, send a file_attachment message via WebSocket, and other participants can download the file.

### Implementation for User Story 3

- [X] T053 [P] [US3] Extend UploadController with file upload endpoint in ChatHub.Api/Controllers/UploadController.cs
- [X] T054 [P] [US3] Implement file validation (size, type) in UploadController.cs
- [X] T055 [US3] Implement direct S3 streaming upload in UploadController.cs
- [X] T056 [US3] Create FileAttachment message handler in ChatHub.Api/Handlers/FileAttachmentHandler.cs
- [X] T057 [US3] Implement file metadata persistence in FileAttachmentHandler
- [X] T058 [US3] Create file download endpoint in ChatHub.Api/Controllers/UploadController.cs
- [X] T059 [US3] Implement pre-signed URL generation for downloads in UploadController.cs

**Checkpoint**: At this point, User Stories 1, 2, AND 3 should work independently. Users can send text, voice, and files.

---

## Phase 6: User Story 4 - See Who Is Online (Priority: P2)

**Goal**: Enable users to see online/offline status of other participants and receive typing indicators when someone is composing a message.

**Independent Test**: When a user joins a service, other participants see their online status. When they type, others see a typing indicator. When they disconnect, status changes to offline.

### Implementation for User Story 4

- [X] T060 [P] [US4] Create presence tracking in Redis in ChatHub.Infrastructure/Cache/RedisPresenceService.cs
- [X] T061 [US4] Implement UserJoined broadcasting on service join in JoinServiceHandler.cs
- [X] T062 [US4] Implement UserLeft broadcasting on service leave/disconnect in ConnectionRegistry cleanup
- [X] T063 [US4] Create Typing message handler in ChatHub.Api/Handlers/TypingHandler.cs
- [X] T064 [US4] Implement typing indicator broadcasting with debouncing in TypingHandler.cs
- [X] T065 [US4] Add presence status to connection registry in ConnectionRegistry.cs
- [X] T066 [US4] Create endpoint to fetch online users in ChatHub.Api/Controllers/PresenceController.cs

**Checkpoint**: At this point, User Stories 1-4 should all work independently. Users can send messages, voice, files, and see presence.

---

## Phase 7: User Story 5 - Reply to Messages (Priority: P3)

**Goal**: Enable users to reply to specific messages, maintaining context and allowing navigation to referenced messages.

**Independent Test**: A user can select a previous message, send a reply linked to it, and other participants see the reply with context preserved.

### Implementation for User Story 5

- [X] T067 [P] [US5] Update TextMessageHandler to support replyToId in ChatHub.Api/Handlers/TextMessageHandler.cs
- [X] T068 [P] [US5] Update VoiceMessageHandler to support replyToId in ChatHub.Api/Handlers/VoiceMessageHandler.cs
- [X] T069 [P] [US5] Update FileAttachmentHandler to support replyToId in ChatHub.Api/Handlers/FileAttachmentHandler.cs
- [X] T070 [US5] Add message threading query endpoint in ChatHub.Api/Controllers/ConversationController.cs
- [X] T071 [US5] Update MessageReceived server message to include reply context in ChatHub.Core/Models/

**Checkpoint**: All user stories should now be independently functional.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [X] T072 [P] Create integration tests for WebSocket connection in ChatHub.Tests/Integration/WebSocketTests.cs
- [X] T073 [P] Create integration tests for NATS backplane in ChatHub.Tests/Integration/NatsTests.cs
- [X] T074 [P] Create integration tests for MongoDB persistence in ChatHub.Tests/Integration/MongoTests.cs
- [X] T075 [P] Create unit tests for message handlers in ChatHub.Tests/Unit/Handlers/
- [X] T076 [P] Add structured logging with correlation IDs across all components
- [X] T077 Add metrics collection: active connections, message throughput, latency in ChatHub.Api/
- [X] T078 Create Kubernetes deployment.yaml manifest in k8s/deployment.yaml
- [X] T079 [P] Create Kubernetes service.yaml manifest in k8s/service.yaml
- [X] T080 [P] Create Kubernetes ingress.yaml manifest in k8s/ingress.yaml
- [X] T081 [P] Create Kubernetes HPA manifest in k8s/hpa.yaml
- [X] T082 [P] Create Kubernetes PDB manifest in k8s/pdb.yaml
- [X] T083 [P] Create Kubernetes ConfigMap in k8s/configmap.yaml
- [X] T084 [P] Create Kubernetes Secret template in k8s/secret.yaml
- [X] T085 [P] Create NATS Helm values in k8s/nats-values.yaml
- [X] T086 Create README.md with architecture overview and usage examples
- [X] T087 Validate quickstart.md steps work end-to-end

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
  - Must complete all T007-T030 before any user story work begins
- **User Stories (Phase 3+)**: All depend on Foundational phase completion
  - User stories can proceed in parallel (if staffed)
  - Or sequentially in priority order (P1 → P2 → P3)
- **Polish (Final Phase)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1 - Text Messages)**: Can start after Foundational (Phase 2)
  - No dependencies on other stories
  - Core messaging functionality - implement first
  
- **User Story 2 (P1 - Voice Messages)**: Can start after Foundational (Phase 2)
  - Depends on User Story 1 infrastructure (optional but recommended)
  - Shares WebSocket infrastructure and handlers pattern
  
- **User Story 3 (P2 - File Sharing)**: Can start after Foundational (Phase 2)
  - Shares upload/download controller pattern
  - Can be implemented in parallel with US1/US2
  
- **User Story 4 (P2 - Presence)**: Can start after Foundational (Phase 2)
  - Depends on connection registry and service join/leave
  - Best implemented after US1 for presence context
  
- **User Story 5 (P3 - Replies)**: Can start after User Story 1
  - Extends message handlers with reply capability
  - Should be implemented after core messaging works

### Within Each User Story

- Models before services
- Services before handlers
- Handlers before endpoints
- Core implementation before integration
- Story complete before moving to next priority

### Parallel Opportunities

- All Setup tasks marked [P] can run in parallel
- All Foundational tasks marked [P] can run in parallel (within Phase 2)
- Once Foundational phase completes, US1 and US2 can start in parallel
- US3 and US4 can start in parallel after Foundational
- US5 should wait until US1 is complete
- All Kubernetes manifest tasks can run in parallel
- All test tasks can run in parallel

---

## Parallel Example: User Story 1

```bash
# Launch all foundational models and interfaces together:
Task: "Create ChatHub.Core settings classes: ChatHubSettings.cs, MongoSettings.cs, NatsSettings.cs, RedisSettings.cs, StorageSettings.cs"
Task: "Create ChatHub.Core interfaces: IConnectionRegistry.cs, IWebSocketSender.cs, INatsBackplane.cs, IMessageDispatcher.cs"
Task: "Create ChatHub.Core interfaces: IMessageRepository.cs, IConversationRepository.cs, IBlobStorageClient.cs, IRateLimiter.cs"

# Once models ready, launch repositories in parallel:
Task: "Implement MessageRepository in ChatHub.Infrastructure/Persistence/MessageRepository.cs"
Task: "Implement ConversationRepository in ChatHub.Infrastructure/Persistence/ConversationRepository.cs"
Task: "Implement S3BlobStorageClient in ChatHub.Infrastructure/Storage/S3BlobStorageClient.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL - blocks all stories)
3. Complete Phase 3: User Story 1 (Text Messaging)
4. **STOP and VALIDATE**: Test User Story 1 independently
   - Connect WebSocket client
   - Join service
   - Send message
   - Verify delivery
5. Deploy/demo if ready

### Incremental Delivery

1. Complete Setup + Foundational → Foundation ready
2. Add User Story 1 → Test independently → Deploy/Demo (MVP!)
3. Add User Story 2 → Test independently → Deploy/Demo
4. Add User Story 3 → Test independently → Deploy/Demo
5. Add User Story 4 → Test independently → Deploy/Demo
6. Add User Story 5 → Test independently → Deploy/Demo
7. Each story adds value without breaking previous stories

### Parallel Team Strategy

With multiple developers:

1. Team completes Setup + Foundational together
2. Once Foundational is done:
   - Developer A: User Story 1 (Text Messaging)
   - Developer B: User Story 2 (Voice Messaging)
   - Developer C: User Story 3 (File Sharing)
   - Developer D: User Story 4 (Presence)
3. Stories complete and integrate independently
4. Developer E: User Story 5 (Replies) after US1 complete
5. Developer F: Tests and Kubernetes manifests in parallel

---

## Task Summary

| Phase | Story | Tasks | Description |
|-------|-------|-------|-------------|
| 1 | - | 6 | Setup - Project structure and dependencies |
| 2 | - | 24 | Foundational - Core infrastructure (BLOCKING) |
| 3 | US1 | 11 | Text messaging - MVP functionality |
| 4 | US2 | 11 | Voice messaging - Live streaming |
| 5 | US3 | 7 | File sharing - Upload and download |
| 6 | US4 | 7 | Presence - Online status and typing |
| 7 | US5 | 5 | Replies - Message threading |
| 8 | - | 16 | Polish - Tests, k8s, docs |
| **Total** | - | **87** | - |

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Verify tests fail before implementing (if test-first approach)
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- Avoid: vague tasks, same file conflicts, cross-story dependencies that break independence
- Follow Constitution: MongoDB before NATS, queue groups for subscription, no blocking I/O
