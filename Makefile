INFRASTRUCTURE_DIR = infrastructure
BACKEND_DIR = backend

INFRASTRUCTURE_PROJECT = $(BACKEND_DIR)/src/AudioProcessor.Infrastructure/AudioProcessor.Infrastructure.csproj
STARTUP_PROJECT = $(BACKEND_DIR)/src/AudioProcessor.Api/AudioProcessor.Api.csproj
SOLUTION_FILE = $(BACKEND_DIR)/AudioProcessor.slnx

CYAN := \033[36m
YELLOW := \033[33m
GREEN := \033[32m
RED := \033[31m
NC := \033[0m

.PHONY: help run migration db-update test \
	docker-build docker-up docker-down \
	docker-build-prod docker-up-prod docker-down-prod

help: ## Show available commands
	@echo ""
	@echo "$(CYAN)Available commands:$(NC)"
	@awk 'BEGIN {FS = ":.*## "} /^[a-zA-Z_-]+:.*## / {printf "$(CYAN)%-25s$(NC) %s\n", $$1, $$2}' $(MAKEFILE_LIST)
	@echo ""

run: ## Run the API locally
	@echo "$(YELLOW)Starting API locally...$(NC)"
	dotnet run --project $(STARTUP_PROJECT)

migration: ## Create a new EF Core migration (example: make migration name=InitialCreate)
	@echo "$(YELLOW)Creating migration $(name)...$(NC)"
	dotnet ef migrations add $(name) \
		--project $(INFRASTRUCTURE_PROJECT) \
		--startup-project $(STARTUP_PROJECT)

db-update: ## Apply pending EF Core migrations
	@echo "$(YELLOW)Updating database...$(NC)"
	dotnet ef database update \
		--project $(INFRASTRUCTURE_PROJECT) \
		--startup-project $(STARTUP_PROJECT)

test: ## Run unit tests
	@echo "$(YELLOW)Running tests...$(NC)"
	dotnet test $(SOLUTION_FILE)

reset-all: ## Clean, rebuild and update database
	@echo "$(RED)Resetting application...$(NC)"
	dotnet clean
	dotnet build
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

docker-build-prod: ## Build production Docker image
	@echo "$(YELLOW)Building production Docker image...$(NC)"
	cd $(INFRASTRUCTURE_DIR) && docker compose \
		-f docker-compose.yml \
		-f docker-compose.prod.yml \
		build

docker-up-prod: ## Start production containers
	@echo "$(GREEN)Starting production containers...$(NC)"
	cd $(INFRASTRUCTURE_DIR) && docker compose \
		-f docker-compose.yml \
		-f docker-compose.prod.yml \
		up -d

docker-down-prod: ## Stop production containers
	@echo "$(RED)Stopping production containers...$(NC)"
	cd $(INFRASTRUCTURE_DIR) && docker compose \
		-f docker-compose.yml \
		-f docker-compose.prod.yml \
		down