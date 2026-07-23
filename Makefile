REGISTRY    ?=
override REGISTRY := $(shell echo "$(REGISTRY)" | sed 's|^https\?://||')
IMAGE_NAME  ?= docmost-mcp
VERSION     := $(shell (git describe --tags --abbrev=0 2>/dev/null || echo "0.0.0-dev") | sed 's/^v//')

.PHONY: help build test docker-build docker-buildx

help:
	@awk -F ':[^#]*?## ' 'NF > 1 {printf "  \033[36m%-22s\033[0m %s\n", $$1, $$2}' $(MAKEFILE_LIST) | sort

build:          ## Build the project (Debug)
	dotnet build

test:           ## Run tests
	dotnet test

docker-build:   ## Build and start containers locally with docker compose
	docker compose up -d --build

docker-buildx:  ## Build both arches (amd64 local + arm64 via builder) and push
	docker buildx build \
		--platform linux/amd64,linux/arm64 \
		-t $(REGISTRY)/$(IMAGE_NAME):$(VERSION) \
		-t $(REGISTRY)/$(IMAGE_NAME):latest \
		-f Dockerfile --push .
