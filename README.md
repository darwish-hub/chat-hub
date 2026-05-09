# ChatHub

A high-performance real-time chat service using native WebSockets, NATS Core pub/sub, MongoDB, and Redis.

## Architecture

- **Transport**: ASP.NET Core native WebSocket middleware (no SignalR)
- **Backplane**: NATS Core pub/sub with queue groups for load-balanced delivery
- **Persistence**: MongoDB (source of truth for all messages)
- **Cache**: Redis for presence, rate limiting, and voice chunk assembly
- **Storage**: S3-compatible (MinIO for local, AWS S3 for production)

## Quick Start

### Local Development

```bash
# Start all dependencies
docker-compose up -d

# The API will be available at http://localhost:8080
# WebSocket endpoint: ws://localhost:8080/ws?token=<jwt>
```

### Build and Run

```bash
# Build the solution
dotnet build ChatHub.sln

# Run the API
dotnet run --project ChatHub.Api
```

## WebSocket Wire Protocol

### Connection

Connect to `/ws?token=<jwt>` with a valid JWT token.

### Client → Server Messages

#### Join Service
```json
{
  "type": "join_service",
  "serviceId": "service-123"
}
```

#### Leave Service
```json
{
  "type": "leave_service",
  "serviceId": "service-123"
}
```

#### Send Text Message
```json
{
  "type": "text_message",
  "id": "msg-uuid",
  "conversationId": "conv-123",
  "serviceId": "service-123",
  "text": "Hello, World!",
  "replyToId": "optional-reply-id"
}
```

#### Send Voice Message (pre-recorded)
```json
{
  "type": "voice_message",
  "id": "msg-uuid",
  "conversationId": "conv-123",
  "blobId": "blob-uuid-from-upload",
  "durationMs": 5000,
  "mimeType": "audio/opus"
}
```

#### Live Voice Streaming
```json
{
  "type": "voice_chunk",
  "id": "msg-uuid",
  "conversationId": "conv-123",
  "sequenceNumber": 0,
  "isFinal": false
}
```
Followed immediately by a binary frame with the audio payload.

#### Send File Attachment
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

#### Typing Indicator
```json
{
  "type": "typing",
  "conversationId": "conv-123",
  "isTyping": true
}
```

#### Acknowledge Message Delivery
```json
{
  "type": "ack",
  "messageId": "msg-uuid"
}
```

#### Pong (response to server ping)
```json
{
  "type": "pong"
}
```

### Server → Client Messages

#### Message Received
```json
{
  "type": "message_received",
  "envelope": {
    "id": "msg-uuid",
    "conversationId": "conv-123",
    "serviceId": "service-123",
    "senderId": "user-123",
    "type": "text",
    "text": "Hello, World!",
    "replyToId": null,
    "createdAt": "2024-01-15T10:30:00Z"
  }
}
```

#### User Joined
```json
{
  "type": "user_joined",
  "userId": "user-123",
  "serviceId": "service-123",
  "displayName": "John Doe"
}
```

#### User Left
```json
{
  "type": "user_left",
  "userId": "user-123",
  "serviceId": "service-123"
}
```

#### Typing Indicator
```json
{
  "type": "typing",
  "userId": "user-123",
  "conversationId": "conv-123",
  "isTyping": true
}
```

#### Delivery Receipt
```json
{
  "type": "delivered",
  "messageId": "msg-uuid"
}
```

#### Error
```json
{
  "type": "error",
  "code": "rate_limit_exceeded",
  "message": "Rate limit exceeded",
  "correlationId": "msg-uuid"
}
```

#### Ping (server sends every 15 seconds)
```json
{
  "type": "ping"
}
```

## Client Connection Examples

### JavaScript

```javascript
const token = 'your-jwt-token';
const ws = new WebSocket(`ws://localhost:8080/ws?token=${token}`);

ws.onopen = () => {
  console.log('Connected');
  
  // Join a service
  ws.send(JSON.stringify({
    type: 'join_service',
    serviceId: 'my-service'
  }));
  
  // Send a message
  ws.send(JSON.stringify({
    type: 'text_message',
    id: crypto.randomUUID(),
    conversationId: 'conv-123',
    serviceId: 'my-service',
    text: 'Hello!'
  }));
};

ws.onmessage = (event) => {
  const message = JSON.parse(event.data);
  console.log('Received:', message);
  
  // Respond to ping
  if (message.type === 'ping') {
    ws.send(JSON.stringify({ type: 'pong' }));
  }
};
```

### Swift (iOS)

```swift
import Foundation

class ChatHubClient: NSObject, URLSessionWebSocketDelegate {
    private var webSocketTask: URLSessionWebSocketTask?
    
    func connect(token: String) {
        let url = URL(string: "ws://localhost:8080/ws?token=\(token)")!
        let request = URLRequest(url: url)
        
        let session = URLSession(configuration: .default, delegate: self, delegateQueue: .main)
        webSocketTask = session.webSocketTask(with: request)
        webSocketTask?.resume()
        
        receiveMessage()
    }
    
    private func receiveMessage() {
        webSocketTask?.receive { [weak self] result in
            switch result {
            case .success(let message):
                switch message {
                case .string(let text):
                    print("Received: \(text)")
                case .data(let data):
                    print("Received binary: \(data)")
                @unknown default:
                    break
                }
                self?.receiveMessage()
            case .failure(let error):
                print("Error: \(error)")
            }
        }
    }
    
    func send(message: [String: Any]) {
        let data = try! JSONSerialization.data(withJSONObject: message)
        let text = String(data: data, encoding: .utf8)!
        webSocketTask?.send(.string(text)) { error in
            if let error = error {
                print("Send error: \(error)")
            }
        }
    }
}
```

### Kotlin (Android)

```kotlin
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.WebSocket
import okhttp3.WebSocketListener
import okio.ByteString

class ChatHubClient {
    private var webSocket: WebSocket? = null
    private val client = OkHttpClient()
    
    fun connect(token: String) {
        val request = Request.Builder()
            .url("ws://localhost:8080/ws?token=$token")
            .build()
        
        webSocket = client.newWebSocket(request, object : WebSocketListener() {
            override fun onOpen(ws: WebSocket, response: okhttp3.Response) {
                println("Connected")
            }
            
            override fun onMessage(ws: WebSocket, text: String) {
                println("Received: $text")
            }
            
            override fun onMessage(ws: WebSocket, bytes: ByteString) {
                println("Received bytes: ${bytes.size}")
            }
            
            override fun onFailure(ws: WebSocket, t: Throwable, response: okhttp3.Response?) {
                println("Error: ${t.message}")
            }
        })
    }
    
    fun send(message: Map<String, Any>) {
        val json = org.json.JSONObject(message).toString()
        webSocket?.send(json)
    }
}
```

## API Endpoints

### File Upload

#### Upload Voice File
```bash
POST /api/upload/voice
Content-Type: multipart/form-data

file: <audio-file>
```

Response:
```json
{
  "blobId": "uuid",
  "fileName": "voice.opus",
  "mimeType": "audio/opus",
  "sizeBytes": 50000
}
```

#### Upload File Attachment
```bash
POST /api/upload/file
Content-Type: multipart/form-data

file: <file>
```

Response:
```json
{
  "blobId": "uuid",
  "fileName": "document.pdf",
  "mimeType": "application/pdf",
  "sizeBytes": 1024000,
  "url": "https://..."
}
```

### Health Checks

- `GET /health` - Liveness probe
- `GET /health/ready` - Readiness probe (checks MongoDB, Redis, NATS)

## Configuration

See `k8s/configmap.yaml` and `k8s/secret.yaml` for all configuration options.

Key settings:
- `ChatHub__MaxMessageSizeBytes`: Maximum WebSocket message size (default: 65536)
- `ChatHub__RateLimitTextPerMinute`: Text message rate limit (default: 100)
- `ChatHub__RateLimitVoicePerMinute`: Voice message rate limit (default: 10)
- `ChatHub__IdleTimeoutMinutes`: Connection idle timeout (default: 30)

## Project Structure

```
ChatHub.sln
├── ChatHub.Core/          # Models, DTOs, interfaces, settings
├── ChatHub.Infrastructure/# WebSockets, NATS, MongoDB, Redis, S3
├── ChatHub.Api/           # Middleware, controllers, health checks
├── ChatHub.Tests/         # Unit and integration tests
└── k8s/                   # Kubernetes manifests
```

## Deployment

### Kubernetes

```bash
# Apply all manifests
kubectl apply -f k8s/

# Or use Helm for NATS
helm install nats nats/nats -f k8s/nats-values.yaml
```

### Docker Compose

```bash
docker-compose up -d
```

## Testing

```bash
# Run all tests
dotnet test ChatHub.Tests

# Run with coverage
dotnet test ChatHub.Tests --collect:"XPlat Code Coverage"
```

## License

MIT
