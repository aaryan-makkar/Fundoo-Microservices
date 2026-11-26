#!/usr/bin/env bash
set -Eeuo pipefail

REPO=/home/azureuser/Fundoo-Microservices
LOCK=/tmp/fundoo-auto-deploy.lock

exec 9>"$LOCK"
flock -n 9 || exit 0

cd "$REPO"

if [[ -n "$(git status --porcelain --untracked-files=normal)" ]]; then
  echo "Refusing deployment: the VM checkout has local changes."
  exit 1
fi

git fetch --quiet --prune origin main
previous_commit=$(git rev-parse HEAD)
target_commit=$(git rev-parse origin/main)

if [[ "$previous_commit" == "$target_commit" ]]; then
  exit 0
fi

echo "Deploying ${target_commit} (from ${previous_commit})"
git reset --hard "$target_commit"

rollback() {
  echo "Deployment failed; restoring ${previous_commit}"
  git reset --hard "$previous_commit"
  sudo -n docker compose build
  sudo -n docker compose up -d --remove-orphans
}

if ! sudo -n docker compose build; then
  rollback
  exit 1
fi

if ! sudo -n docker compose up -d --remove-orphans; then
  rollback
  exit 1
fi

for attempt in $(seq 1 30); do
  if curl --fail --silent http://127.0.0.1:8080/health >/dev/null; then
    echo "Deployment healthy at ${target_commit}"
    exit 0
  fi
  sleep 5
done

sudo -n docker compose ps
sudo -n docker compose logs --tail=100 api-gateway user-api notes-api notification-api
rollback
exit 1
