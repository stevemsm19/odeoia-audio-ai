INFRASTRUCTURE_DIR = infrastructure
BACKEND_DIR = backend

INFRASTRUCTURE_PROJECT = $(BACKEND_DIR)/src/AudioProcessor.Infrastructure/AudioProcessor.Infrastructure.csproj
STARTUP_PROJECT = $(BACKEND_DIR)/src/AudioProcessor.Api/AudioProcessor.Api.csproj
SOLUTION_FILE = $(BACKEND_DIR)/AudioProcessor.sln

CYAN := \033[36m
YELLOW := \033[33m
GREEN := \033[32m
RED := \033[31m
NC := \033[0m

.PHONY: help init init-docker setup-hooks format lint build run migration db-update test reset-all docker-build docker-up docker-down

help: ## Show available commands
	@echo ""
	@echo "$(CYAN)Available commands:$(NC)"
	@awk 'BEGIN {FS = ":.*## "} /^[a-zA-Z_-]+:.*## / {printf "$(CYAN)%-25s$(NC) %s\n", $$1, $$2}' $(MAKEFILE_LIST)
	@echo ""

init: setup-hooks run ## Initialize Local Development (Setup hooks and run API)

init-docker: setup-hooks docker-up ## Initialize Containerized Development (Setup hooks and run Docker)

validate: lint build test ## Validate code quality

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