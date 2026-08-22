INFRASTRUCTURE_DIR = infrastructure
BACKEND_DIR = backend
FRONTEND_DIR = frontend

INFRASTRUCTURE_PROJECT = $(BACKEND_DIR)/src/AudioProcessor.Infrastructure/AudioProcessor.Infrastructure.csproj
STARTUP_PROJECT = $(BACKEND_DIR)/src/AudioProcessor.Api/AudioProcessor.Api.csproj
SOLUTION_FILE = $(BACKEND_DIR)/AudioProcessor.sln

CYAN := \033[36m
YELLOW := \033[33m
GREEN := \033[32m
RED := \033[31m
NC := \033[0m

.PHONY: help init init-docker setup-hooks download-models validate check backend-check backend-lint backend-build backend-test frontend-install frontend-dev frontend-lint frontend-build frontend-test frontend-check format lint build run migration db-update test reset-all docker-build docker-up docker-down benchmark

help: ## Show available commands
	@echo ""
	@echo "$(CYAN)Available commands:$(NC)"
	@awk 'BEGIN {FS = ":.*## "} /^[a-zA-Z_-]+:.*## / {printf "$(CYAN)%-25s$(NC) %s\n", $$1, $$2}' $(MAKEFILE_LIST)
	@echo ""

init: setup-hooks download-models run ## Initialize Local Development (Setup hooks, models, and run API)

init-docker: setup-hooks download-models docker-up ## Initialize Containerized Development (Setup hooks, models, and run Docker)

download-models: ## Provision missing local Whisper and GGUF models
	./scripts/download-models.sh

validate: check ## Validate the complete repository

check: backend-check frontend-check ## Validate backend and frontend

backend-check: backend-lint backend-build backend-test ## Validate the backend

backend-lint: ## Validate backend code style
	@$(MAKE) lint

backend-build: ## Build the backend in Release mode
	@$(MAKE) build

backend-test: ## Run backend unit tests
	@$(MAKE) test

frontend-install: ## Install frontend dependencies from the lockfile
	npm ci --prefix $(FRONTEND_DIR)

frontend-dev: ## Start the Vite frontend development server
	npm run dev --prefix $(FRONTEND_DIR)

frontend-lint: ## Lint frontend source files
	npm run lint --prefix $(FRONTEND_DIR)

frontend-build: ## Create the frontend production build
	npm run build --prefix $(FRONTEND_DIR)

frontend-test: ## Run frontend unit tests (Vitest)
	npm run test --prefix $(FRONTEND_DIR)

frontend-check: frontend-lint frontend-build frontend-test ## Validate the frontend

setup-hooks: ## Set up local Git pre-push hooks
	@echo "$(YELLOW)Setting up Git pre-push hook...$(NC)"
	@mkdir -p .git/hooks
	@cp scripts/git-hooks/pre-push .git/hooks/pre-push 2>/dev/null || :
	@chmod +x .git/hooks/pre-push 2>/dev/null || :
	@echo "$(GREEN)Git pre-push hook configured successfully!$(NC)"

format: ## Automatically fix code style issues
	@echo "$(YELLOW)Formatting code...$(NC)"
	dotnet format $(SOLUTION_FILE)

lint: ## Validate code style (fails if changes are needed)
	@echo "$(YELLOW)Linting code...$(NC)"
	dotnet format $(SOLUTION_FILE) --verify-no-changes

build: ## Build the solution in Release mode
	@echo "$(YELLOW)Building solution...$(NC)"
	dotnet build $(SOLUTION_FILE) -c Release

run: ## Run the API locally
	@echo "$(YELLOW)Starting API locally...$(NC)"
	dotnet run --project $(STARTUP_PROJECT)

migration: ## Create a new EF Core migration (example: make migration name=InitialCreate)
	@echo "$(YELLOW)Creating migration $(name)...$(NC)"
	dotnet ef migrations add $(name) \
		--project $(INFRASTRUCTURE_PROJECT) \
		--startup-project $(STARTUP_PROJECT)

db-update: ## Apply pending EF Core migrations manually
	@echo "$(YELLOW)Updating database...$(NC)"
	dotnet ef database update \
		--project $(INFRASTRUCTURE_PROJECT) \
		--startup-project $(STARTUP_PROJECT)

test: ## Run unit tests
	@echo "$(YELLOW)Running tests...$(NC)"
	dotnet test $(SOLUTION_FILE)

reset-all: ## Clean, rebuild and update database
	@echo "$(RED)Resetting application...$(NC)"
	dotnet clean $(SOLUTION_FILE)
	dotnet build $(SOLUTION_FILE)
	dotnet ef database update \
		--project $(INFRASTRUCTURE_PROJECT) \
		--startup-project $(STARTUP_PROJECT)

docker-build: ## Build Docker image for development
	@echo "$(YELLOW)Building development Docker image...$(NC)"
	cd $(INFRASTRUCTURE_DIR) && docker compose build

docker-up: ## Start development containers
	@echo "$(GREEN)Starting development containers...$(NC)"
	cd $(INFRASTRUCTURE_DIR) && docker compose up --build

docker-down: ## Stop development containers
	@echo "$(RED)Stopping development containers...$(NC)"
	cd $(INFRASTRUCTURE_DIR) && docker compose down

benchmark: ## Run a k6 upload benchmark (example: make benchmark LABEL=tpl-sem3-vu20 VUS=20)
	@if [ -z "$(LABEL)" ] || [ -z "$(VUS)" ]; then \
		echo "$(RED)Usage: make benchmark LABEL=<label> VUS=<count>$(NC)"; \
		exit 1; \
	fi
	@echo "$(YELLOW)Running benchmark '$(LABEL)' with $(VUS) VUs...$(NC)"
	$(INFRASTRUCTURE_DIR)/benchmarks/run-benchmark.sh "$(LABEL)" "$(VUS)"
