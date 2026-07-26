# Audio Processor API

This interactive API is designed to process, compress, filter, and summarize voice audio files efficiently using Clean Architecture.

## Project Structure

```text
/backend            → C# .NET 8 Web API (Clean Architecture)
/frontend           → TypeScript client (future)
/infrastructure     → Docker, .env, and this README
Makefile            → Developer automation commands
```

## Prerequisites

| Tool           | Version  | Description                         |
| -------------- | -------- | ----------------------------------- |
| .NET SDK       | 8.0      | Backend development                 |
| FFmpeg         | Any      | Required for local audio compression|
| Docker         | 24+      | Container execution                 |
| Docker Compose | v2.x     | Docker orchestration                |
| make           | Any      | Developer automation commands       |
| WSL            | Optional | Recommended for Windows development |

---

# Quick Start Guide

> [!WARNING]  
> **MANDATORY PREREQUISITE:** Before executing any setup commands or running the application, you **must** configure your environment variables. The API will fail to start if the database and Supabase credentials are not provided.

### Step 1: Configure Environment Variables

All secrets and connection strings are managed through the `.env` file. You need to create this file by copying the provided example.

**For macOS / Linux / Windows WSL:**
```bash
cp infrastructure/.env.example infrastructure/.env
```

**For Windows PowerShell:**
```powershell
Copy-Item infrastructure\.env.example infrastructure\.env
```

Open `/infrastructure/.env` and fill in your Supabase and PostgreSQL credentials:
```env
BUCKET=audio-files
DB_CONNECTION_STRING=
SUPABASE_URL=
SUPABASE_KEY=
```

---

### Step 2: Initialize and Run the Application

Database migrations are handled automatically on startup during local development, so you only need to run a single command to get everything working. Choose **Option A** or **Option B** depending on your preferred workflow.

#### Option A: Local Development (Bare Metal / WSL)
*Recommended for active coding and debugging.*

**macOS / Linux / Windows WSL:**
```bash
make init
```

**Windows PowerShell (Manual Equivalent):**
```powershell
dotnet run --project backend\src\AudioProcessor.Api
```

#### Option B: Containerized Development (Docker)
*Recommended for testing isolated environments without installing the .NET SDK.*

**macOS / Linux / Windows WSL:**
```bash
make init-docker
```

**Windows PowerShell (Manual Equivalent):**
```powershell
cd infrastructure
docker compose up --build
```

---

### Step 3: Access Endpoints

Once the application is running, it will automatically apply migrations and expose the following local URLs depending on how you started it:

**Option A (Local Running - Port 5196)**
- **API Base URL:** `http://localhost:5196`
- **Swagger UI (Testing Interface):** `http://localhost:5196/swagger`

**Option B (Docker Running - Port 8080)**
- **API Base URL:** `http://localhost:8080`
- **Swagger UI (Testing Interface):** `http://localhost:8080/swagger`

---

## Quality Assurance & Code Style

We use `make` targets to enforce code quality locally and in our CI/CD pipeline. Run these commands before pushing changes:

| Command        | Description                                               |
| -------------- | --------------------------------------------------------- |
| `make format`  | Automatically fixes formatting and style issues globally. |
| `make lint`    | Validates code style (fails if changes are needed).       |
| `make build`   | Compiles the solution in Release mode to ensure zero bugs.|
| `make test`    | Executes the full suite of xUnit tests.                   |

---

## Developer Operations (Makefile)

If you are using macOS, Linux, or WSL, you can use the `Makefile` for standalone tasks. Here is the complete list of available commands:

| Command                               | Description                                         |
| ------------------------------------- | --------------------------------------------------- |
| `make help`                           | Show all available commands                         |
| `make init`                           | Full local setup (Hooks + Run API)                  |
| `make init-docker`                    | Full Docker setup (Hooks + Start Containers)        |
| `make setup-hooks`                    | Set up local Git pre-push hooks                     |
| `make format`                         | Automatically fix code style issues                 |
| `make lint`                           | Validate code style (fails if changes are needed)   |
| `make build`                          | Build the solution in Release mode                  |
| `make run`                            | Run the API locally                                 |
| `make migration name=<Name>`          | Create a new EF Core migration                      |
| `make db-update`                      | Apply pending EF Core migrations manually           |
| `make test`                           | Run all unit tests                                  |
| `make reset-all`                      | Clean, rebuild, and update database manually        |
| `make docker-build`                   | Build development Docker image                      |
| `make docker-up`                      | Start development Docker container                  |
| `make docker-down`                    | Stop development Docker container                   |

---

## API Endpoints

| Method | Endpoint            | Description                           |
| ------ | ------------------- | ------------------------------------- |
| POST   | `/api/audio/upload` | Upload an `.mp3` or `.wav` audio file |

**Example request (using Docker port):**
```bash
curl -X POST http://localhost:8080/api/audio/upload \
  -F "file=@/path/to/audio.mp3"
```

**Example response:**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "fileName": "audio.mp3",
  "storageUrl": "https://your-project.supabase.co/storage/v1/object/public/audio-files/uuid/audio.mp3",
  "createdAt": "2025-01-01T00:00:00Z"
}
```

---

## Architecture Overview

This project strictly follows **Clean Architecture**:

```text
Domain
 └── Core entities, value objects, and pure domain exceptions (No external dependencies)

Application
 └── Use cases, services, and interface ports (Strictly isolated from Infrastructure)

Infrastructure
 └── EF Core DB context, migrations, Supabase storage, and external adapters

Api
 └── HTTP controllers, minimal APIs, dependency injection container (`Program.cs`)

Tests
 └── Focused unit tests for domain and application layers
```