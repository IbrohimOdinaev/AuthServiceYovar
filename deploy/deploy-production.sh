#!/usr/bin/env bash
set -euo pipefail

WORKDIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
COMPOSE_FILE="${COMPOSE_FILE:-docker-compose.production.yml}"
AUTHSERVICE_IMAGE="${AUTHSERVICE_IMAGE:-ghcr.io/ibrohimodinaev/authserviceyovar:latest}"

cd "$WORKDIR"

echo "Using compose file: $COMPOSE_FILE"
echo "Using image: $AUTHSERVICE_IMAGE"

if [[ "${1:-}" == "--pull-image" ]]; then
  docker pull "$AUTHSERVICE_IMAGE"
fi

AUTHSERVICE_IMAGE="$AUTHSERVICE_IMAGE" \
docker compose -f "$COMPOSE_FILE" up -d --pull always --remove-orphans

echo "Compose services status:"
docker compose -f "$COMPOSE_FILE" ps

echo "Last migrate logs:"
docker logs --tail 40 authservice-migrate || true

echo "Auth service logs (tail):"
docker logs --tail 40 authservice-api || true
