# OdeoIA

OdeoIA is a full-stack audio-processing application. The .NET 8 API follows Clean Architecture and performs asynchronous audio compression and AI-powered summarization in a background worker. The React/Vite frontend uploads audio, polls the real job status, and plays the processed AAC file alongside the generated summary when it is ready.

## Repository Structure

```text
backend/         .NET 8 API — Clean Architecture, use cases, xUnit tests
frontend/        React + Vite client
infrastructure/  Docker Compose, API Dockerfile, Nginx proxy configuration
workers/         Python worker script for local Whisper + LLM summarization
scripts/         Git hooks and model download automation
models/          Local AI models (Whisper + GGUF) — provisioned by init-docker
```

---

## Prerequisites

| Tool           | Version  | Notes                                                      |
| -------------- | -------- | ---------------------------------------------------------- |
| .NET SDK       | 8.0      | Required for local (bare-metal) development only           |
| Node.js + npm  | 24+      | Required for local frontend development only               |
| FFmpeg         | Any      | Required for local audio compression                       |
| Docker Desktop | 24+      | Required for containerized development                     |
| Docker Compose | v2.x     | Bundled with Docker Desktop                                |
| `make`         | Any      | Required to run all automation commands (see note below)   |

> [!IMPORTANT]
> **Windows users:** `make` is not available natively on Windows. Install it through **WSL 2** (Windows Subsystem for Linux) or Git Bash. WSL 2 is the recommended approach as it provides a full Linux environment compatible with all project commands.
>
> Install WSL: `wsl --install` in PowerShell (Administrator), then use the Ubuntu terminal for all `make` commands.

---

## Quick Start

> [!WARNING]
> **Configure your environment variables first.** The API will fail to start without the database and Supabase credentials. Complete **Step 1** before running any other command.

### Step 1 — Configure Environment Variables

Copy the provided templates and fill in your credentials. **Do not commit either file.**

**macOS / Linux / WSL:**
```bash
cp infrastructure/.env.example infrastructure/.env
cp frontend/.env.example frontend/.env
```

**Windows PowerShell:**
```powershell
Copy-Item infrastructure\.env.example infrastructure\.env
Copy-Item frontend\.env.example frontend\.env
```

Open `infrastructure/.env` and provide your Supabase and PostgreSQL credentials:

```env
BUCKET=audio-files
DB_CONNECTION_STRING=Host=...;Database=...;Username=...;Password=...
SUPABASE_URL=https://your-project.supabase.co
SUPABASE_KEY=your-service-role-key
FRONTEND_ORIGIN=http://localhost:5173
```

The frontend uses these non-secret Vite values (already set in `frontend/.env.example`):

| Variable             | Default                   | Purpose                          |
| -------------------- | ------------------------- | -------------------------------- |
| `VITE_API_BASE_URL`  | `/api`                    | Browser-facing API path          |
| `VITE_DEV_API_TARGET`| `http://localhost:5196`   | Local Vite proxy target          |

---

### Step 2 — Initialize and Run

Choose the workflow that matches your environment.

#### Option A: Docker (Recommended for first-time setup)

Runs the full stack in containers. No .NET SDK or FFmpeg installation required.

**macOS / Linux / WSL:**
```bash
make init-docker
```

This single command:
1. Installs the Git pre-push hook
2. Downloads the required local AI models (Whisper + GGUF) into `models/`
3. Builds and starts all Docker containers

**Windows PowerShell (manual equivalent):**
```powershell
cd infrastructure
docker compose up --build
```

#### Option B: Local Development (Bare Metal)

Recommended for active coding and step-through debugging.

**macOS / Linux / WSL:**
```bash
make init
```

This installs hooks, downloads models, and starts the API locally. Then in a separate terminal:

```bash
make frontend-dev
```

---

### Step 3 — Access the Application

| Mode              | Frontend                      | API                           | Swagger UI                         |
| ----------------- | ----------------------------- | ----------------------------- | ---------------------------------- |
| **Docker**        | `http://localhost:5173`       | `http://localhost:8080`       | `http://localhost:8080/swagger`    |
| **Local (API)**   | `http://localhost:5173`       | `http://localhost:5196`       | `http://localhost:5196/swagger`    |

Database migrations are applied automatically on startup. No manual migration step is needed for first-time setup.

---

## Configuration Reference

### `infrastructure/.env`

| Variable              | Required | Description                              |
| --------------------- | -------- | ---------------------------------------- |
| `BUCKET`              | ✅       | Supabase storage bucket name             |
| `DB_CONNECTION_STRING`| ✅       | PostgreSQL connection string             |
| `SUPABASE_URL`        | ✅       | Supabase project URL                     |
| `SUPABASE_KEY`        | ✅       | Supabase service role key                |
| `FRONTEND_ORIGIN`     | ❌       | CORS allowed origin (default: `http://localhost:5173`) |

---

## API Reference

| Method | Endpoint              | Description                                              |
| ------ | --------------------- | -------------------------------------------------------- |
| `POST` | `/api/audio/upload`   | Upload an `.mp3` or `.wav` file; returns `202 Accepted` |
| `GET`  | `/api/audio/{id}`     | Poll job status, compression URL, and summary           |

**Example upload:**
```bash
curl -X POST http://localhost:8080/api/audio/upload \
  -F "file=@/path/to/audio.mp3"
```

**Example status response:**
```json
{
  "id": "6730c377-a21c-4980-80c9-f0b77f9e6d7a",
  "fileName": "audio.mp3",
  "compressionStatus": "Completed",
  "compressedStorageUrl": "https://your-project.supabase.co/storage/v1/object/public/...",
  "summaryStatus": "Completed",
  "summary": "The speaker discussed quarterly results and outlined the roadmap for Q3.",
  "summaryError": null
}
```

The API accepts `.mp3` and `.wav` files up to 50 MB. Upload returns immediately while the background service compresses the file and generates a summary in parallel. The frontend polls the status endpoint until both `compressionStatus` and `summaryStatus` reach a terminal state (`Completed` or `Failed`).

---

## Quality Assurance

Run these commands before pushing changes. The pre-push hook enforces them automatically.

| Command        | Description                                               |
| -------------- | --------------------------------------------------------- |
| `make format`  | Automatically fixes formatting and style issues globally. |
| `make lint`    | Validates code style (fails if changes are needed).       |
| `make build`   | Compiles the solution in Release mode.                    |
| `make test`    | Executes the full suite of xUnit tests.                   |
| `make check`   | Runs all backend and frontend validations.                |

---

## Makefile Reference

All automation is provided through `make`. Run `make help` to list all available targets.

| Command                        | Description                                          |
| ------------------------------ | ---------------------------------------------------- |
| `make help`                    | Show all available commands                          |
| `make init`                    | Local setup: hooks + models + run API                |
| `make init-docker`             | Docker setup: hooks + models + build + start         |
| `make setup-hooks`             | Install the Git pre-push hook                        |
| `make download-models`         | Download missing Whisper and GGUF models             |
| `make format`                  | Auto-fix code style issues                           |
| `make lint`                    | Validate code style (fails if changes are needed)    |
| `make build`                   | Build the solution in Release mode                   |
| `make run`                     | Run the API locally                                  |
| `make migration name=<Name>`   | Create a new EF Core migration                       |
| `make db-update`               | Apply pending EF Core migrations manually            |
| `make test`                    | Run all unit tests                                   |
| `make reset-all`               | Clean, rebuild, and update the database              |
| `make docker-build`            | Build Docker images                                  |
| `make docker-up`               | Start all Docker containers                          |
| `make docker-down`             | Stop all Docker containers                           |
| `make backend-check`           | Run backend lint + build + tests                     |
| `make frontend-install`        | Install frontend npm dependencies                    |
| `make frontend-dev`            | Start the Vite dev server                            |
| `make frontend-check`          | Run frontend lint + production build                 |

---

## Pre-push Hook & CI/CD

The pre-push hook detects which parts of the repository changed and runs only the relevant validations:

- `backend/**` changes → `make backend-check`
- `frontend/**` changes → `make frontend-check`
- Shared files (`Makefile`, Compose, CI, scripts) → both checks
- Documentation-only changes → no application validation
- If the commit range cannot be determined → both checks

GitLab CI applies the same path-aware strategy using `rules:changes`. Backend and frontend pipelines are fully independent and run in parallel.

---

## Architecture

```text
React UI → frontend service layer → /api (Nginx proxy) → .NET API
                                                        → Background Queue
                                                              → FFmpeg (compression)
                                                              → Whisper + LLM (summarization)
                                                              → Supabase Storage
```

This project strictly follows **Clean Architecture**:

```text
Domain          Core entities, value objects, and domain exceptions (zero dependencies)
Application     Use cases, ports/interfaces (strictly isolated from Infrastructure)
Infrastructure  EF Core, Supabase storage, FFmpeg, Whisper/LLM subprocess adapters
Api             HTTP controllers, DI container, background worker host (Program.cs)
Tests           Focused unit tests for domain and application layers
```
