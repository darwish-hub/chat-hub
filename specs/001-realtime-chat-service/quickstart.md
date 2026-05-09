# Quick Start: ChatHub Development

**Date**: 2026-05-09  
**Purpose**: Get the chat service running locally in under 5 minutes

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- [Docker](https://docs.docker.com/get-docker/) and Docker Compose
- Git

## 1. Start Dependencies

All infrastructure dependencies run via Docker Compose:

```bash
docker-compose up -d
```

This starts:
- **MongoDB** (port 27017) - Message persistence
- **Redis** (port 6379) - Caching and presence
- **NATS** (port 4222) - Cross-pod message fan-out
- **MinIO** (port 9000/9001) - S3-compatible file storage

**Verify services are healthy**:
```bash
docker-compose ps
```

## 2. Configure Environment

Copy the example environment file:

```bash
cp .env.example .env
```

Edit `.env` with your values:

```env
# JWT (generate a secure key)
JWT_SIGNING_KEY=your-256-bit-secret-key-here

# MongoDB
MONGO_CONNECTION_STRING=mongodb://localhost:27017/chathub

# Redis
REDIS_CONNECTION_STRING=localhost:6379

# NATS
NATS_URL=nats://localhost:4222

# S3/MinIO
S3_ENDPOINT=http://localhost:9000
S3_ACCESS_KEY=minioadmin
S3_SECRET_KEY=minioadmin
S3_BUCKET=chathub-uploads
```

## 3. Build and Run

Restore dependencies and build:

```bash
dotnet restore
dotnet build
```

Run the API:

```bash
dotnet run --project ChatHub.Api
```

The service will be available at `http://localhost:8080`

## 4. Verify Installation

### Health Check

```bash
curl http://localhost:8080/health
```

Expected response:
```json
{"status":"healthy"}
```

### WebSocket Connection Test

Using [wscat](https://github.com/websockets/wscat):

```bash
# Install wscat
npm install -g wscat

# Connect (you'll need a valid JWT token)
wscat -c "ws://localhost:8080/ws?token=YOUR_JWT_TOKEN"

# Send a ping
> {"type":"pong"}
```

### API Test

```bash
# Upload a file (requires auth)
curl -X POST http://localhost:8080/api/upload/file \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -F "file=@test.txt"
```

## 5. Development Workflow

### Hot Reload

Run with hot reload for development:

```bash
dotnet watch --project ChatHub.Api
```

### Run Tests

```bash
# All tests
dotnet test

# With coverage
dotnet test --collect:"XPlat Code Coverage"

# Specific test project
dotnet test ChatHub.Tests
```

### View Logs

```bash
# Docker logs
docker-compose logs -f

# Application logs (structured JSON)
dotnet run --project ChatHub.Api -- --logging:console:format=Json
```

## 6. Local Testing with MinIO

Access MinIO console at `http://localhost:9001`
- Default credentials: `minioadmin` / `minioadmin`
- Create bucket: `chathub-uploads`
- Make bucket public for local dev (optional)

## 7. Connect a Client

### JavaScript Example

```javascript
const token = 'your-jwt-token';
const ws = new WebSocket(`ws://localhost:8080/ws?token=${token}`);

ws.onopen = () => {
  console.log('Connected');
  
  // Join a service
  ws.send(JSON.stringify({
    type: 'join_service',
    serviceId: 'test-service'
  }));
  
  // Send a message
  ws.send(JSON.stringify({
    type: 'text_message',
    id: crypto.randomUUID(),
    conversationId: 'test-conv',
    serviceId: 'test-service',
    text: 'Hello from browser!'
  }));
};

ws.onmessage = (event) => {
  const msg = JSON.parse(event.data);
  console.log('Received:', msg);
  
  if (msg.type === 'ping') {
    ws.send(JSON.stringify({ type: 'pong' }));
  }
};
```

## Troubleshooting

### Connection Refused

```bash
# Check if services are running
docker-compose ps

# Restart services
docker-compose restart

# Check logs
docker-compose logs nats
docker-compose logs mongo
docker-compose logs redis
```

### WebSocket Authentication Failed

- Ensure JWT token is valid and not expired
- Check token is URL-encoded if it contains special characters
- Verify JWT_SIGNING_KEY matches between token generator and API

### MongoDB Connection Issues

```bash
# Check MongoDB is accepting connections
docker-compose exec mongo mongosh --eval "db.adminCommand('ping')"
```

### Port Conflicts

If ports are already in use, modify `docker-compose.yml`:

```yaml
services:
  mongo:
    ports:
      - "27018:27017"  # Use different host port
```

## Next Steps

- Review the [WebSocket Protocol](./contracts/websocket-protocol.md)
- Explore the [REST API](./contracts/rest-api.md)
- Read the [Data Model](./data-model.md)
- Check out the [Implementation Plan](../plan.md)

## Project Structure

```
ChatHub/
├── ChatHub.Api/          # Web API and middleware
├── ChatHub.Core/         # Models, interfaces, settings
├── ChatHub.Infrastructure/# Implementations
├── ChatHub.Tests/        # Unit and integration tests
├── k8s/                  # Kubernetes manifests
├── docker-compose.yml    # Local infrastructure
└── .env.example          # Environment template
```

## Useful Commands

```bash
# Stop all services
docker-compose down

# Reset all data (WARNING: destructive)
docker-compose down -v
docker-compose up -d

# View MongoDB data
docker-compose exec mongo mongosh chathub --eval "db.messages.find().limit(5)"

# Monitor NATS traffic
docker-compose exec nats nats sub ">"

# Check Redis keys
docker-compose exec redis redis-cli KEYS '*'

# Build Docker image
docker build -t chathub-api:latest -f ChatHub.Api/Dockerfile .
```
