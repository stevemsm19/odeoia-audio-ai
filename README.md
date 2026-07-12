# Audio Processor API

This interactive api is designed to process, compress, filter, and summarize voice audio files efficiently.

## Project Structure

```
/backend            → C# .NET 10 Web API (Clean Architecture)
/frontend           → TypeScript client (future)
/infrastructure     → Docker, .env, and this README
Makefile            → Developer automation commands
```

# Prerequisites

| Tool           | Version  | Description                         |
| -------------- | -------- | ----------------------------------- |
| .NET SDK       | 10.0     | Backend development                 |
| Docker         | 24+      | Container execution                 |
| Docker Compose | v2.x     | Docker orchestration                |
| make           | Any      | Developer automation commands       |
| WSL            | Optional | Recommended for Windows development |

---

# Environment Configuration

All secrets and connection strings are managed through the `.env` file located at:

```text
/infrastructure/.env
```

Create your local environment file:

## macOS / Linux

```bash
cp infrastructure/.env.example infrastructure/.env
```

## Windows PowerShell

```powershell
Copy-Item infrastructure\.env.example infrastructure\.env
```

Update the file with your credentials:

```env
DB_CONNECTION_STRING=
SUPABASE_URL=
SUPABASE_KEY=
```

---

# Local Development

The project includes a `Makefile` to simplify common development tasks.

To see all available commands:

```bash
make help
```

---

# macOS / Linux

The Makefile commands work directly.

Install EF Core CLI tools if needed:

```bash
dotnet tool install --global dotnet-ef
```

Create a new migration:

```bash
make migration name=InitialCreate
```

Apply database migrations:

```bash
make db-update
```

Run the API locally:

```bash
make run
```

Run unit tests:

```bash
make test
```

The API will be available at:

```text
http://localhost:8080
```

Swagger UI:

```text
http://localhost:8080/swagger
```

---

# Windows

## Option 1: Using WSL (Recommended)

Install and use Windows Subsystem for Linux.

Inside the WSL terminal, all Makefile commands work the same:

```bash
make migration name=InitialCreate
make db-update
make run
make test
```

---

## Option 2: Using PowerShell

If you are not using WSL, execute the equivalent commands manually.

Install EF Core CLI:

```powershell
dotnet tool install --global dotnet-ef
```

Create a migration:

```powershell
dotnet ef migrations add InitialCreate `
  --project backend\src\AudioProcessor.Infrastructure `
  --startup-project backend\src\AudioProcessor.Api
```

Apply database migrations:

```powershell
dotnet ef database update `
  --project backend\src\AudioProcessor.Infrastructure `
  --startup-project backend\src\AudioProcessor.Api
```

Run the API:

```powershell
dotnet run --project backend\src\AudioProcessor.Api
```

Run tests:

```powershell
dotnet test backend\AudioProcessor.slnx
```

---

# Docker Development

Docker is supported on macOS, Linux, and Windows using Docker Desktop.

The development Docker configuration uses:

```text
infrastructure/docker-compose.yml
```

## Build Docker image

Using Make:

```bash
make docker-build
```

Equivalent command:

```bash
cd infrastructure
docker compose build
```

---

## Start Docker container

Using Make:

```bash
make docker-up
```

Equivalent command:

```bash
cd infrastructure
docker compose up --build
```

---

## Stop Docker container

Using Make:

```bash
make docker-down
```

Equivalent command:

```bash
cd infrastructure
docker compose down
```

The API will be available at:

```text
http://localhost:8080
```

Swagger UI:

```text
http://localhost:8080/swagger
```

---

# Makefile Commands

| Command                               | Description                        |
| ------------------------------------- | ---------------------------------- |
| `make help`                           | Show available commands            |
| `make run`                            | Run the API locally                |
| `make migration name=<MigrationName>` | Create a new EF Core migration     |
| `make db-update`                      | Apply pending database migrations  |
| `make test`                           | Run unit tests                     |
| `make reset-all`                      | Clean, rebuild and update database |
| `make docker-build`                   | Build development Docker image     |
| `make docker-up`                      | Start development Docker container |
| `make docker-down`                    | Stop development Docker container  |

---

# EF Core Migrations

The project uses Entity Framework Core migrations to manage database schema changes.

Create a migration:

```bash
make migration name=MigrationName
```

Equivalent command:

```bash
dotnet ef migrations add MigrationName `
  --project backend\src\AudioProcessor.Infrastructure `
  --startup-project backend\src\AudioProcessor.Api
```

Apply migrations:

```bash
make db-update
```

Equivalent command:

```bash
dotnet ef database update `
  --project backend\src\AudioProcessor.Infrastructure `
  --startup-project backend\src\AudioProcessor.Api
```

Migration files are stored in:

```text
backend/src/AudioProcessor.Infrastructure/Persistence/Migrations
```

---

# Running Tests

Execute unit tests:

```bash
make test
```

Equivalent command:

```bash
dotnet test backend/AudioProcessor.slnx
```

---

# API Endpoints

| Method | Endpoint            | Description                           |
| ------ | ------------------- | ------------------------------------- |
| POST   | `/api/audio/upload` | Upload an `.mp3` or `.wav` audio file |

Example request:

```bash
curl -X POST http://localhost:8080/api/audio/upload \
  -F "file=@/path/to/audio.mp3"
```

Example response:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "fileName": "audio.mp3",
  "storageUrl": "https://your-project.supabase.co/storage/v1/object/public/audio-files/uuid/audio.mp3",
  "createdAt": "2025-01-01T00:00:00Z"
}
```

---

# Architecture Overview

This project follows **Clean Architecture**:

```text
Domain
 └── Core entities and business rules

Application
 └── Use cases and interfaces

Infrastructure
 └── Database, EF Core, repositories and external services

Api
 └── HTTP controllers, dependency injection and application startup

Tests
 └── Unit tests
```