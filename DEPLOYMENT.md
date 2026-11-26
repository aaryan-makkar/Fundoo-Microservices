# Fundoo Azure VM deployment

This runbook is tailored to the repository and the Ubuntu 24.04 Fundoo VM shown in the Azure screenshot. It starts with Docker Compose so the APIs and database can be brought up on the VM without a registry. The attached guide's later K3s and Jenkins stages can be layered on once this deployment works.

## What this deploys

- API Gateway on TCP port 8080
- User, Notes, and Notification APIs on the private Compose network
- PostgreSQL 16 with separate `fundoo_users` and `fundoo_notes` databases
- Initial database schema for both services

The gateway uses a deployment-specific Ocelot config in `deploy/ocelot.docker.json`; the development config points at `localhost` and HTTPS ports, which do not work between containers. SMTP is required by the Notification API's startup validation.

## 1. Prepare the VM

In Azure Networking, allow inbound TCP 22 from your current IP and TCP 8080 from the client addresses that should reach the API. Do not expose PostgreSQL (5432) or the API service ports. For production with a domain, put HTTPS reverse proxying in front of port 8080 and only expose 80/443 publicly. Azure NSGs filter inbound traffic, so the rule must be present on the effective NSG attached to the VM/NIC or subnet. See [Azure NSG overview](https://learn.microsoft.com/azure/virtual-network/network-security-groups-overview).

Connect to the machine using the SSH private key already on your computer (do not copy it into the repository or chat). Install Docker Engine and its Compose plugin using Docker's official [Ubuntu instructions](https://docs.docker.com/engine/install/ubuntu/), then confirm `docker compose version` works.

## 2. Put the source on the VM

The deployment source is stored in GitHub. On a fresh VM, run:

```bash
git clone https://github.com/aaryan-makkar/Fundoo-Microservices.git
cd Fundoo-Microservices
```

If the repo is private, configure Git access on the VM using your own secure method. Do not put a token in the clone URL. The VM must have the new `Dockerfile`, `.dockerignore`, `docker-compose.yml`, `deploy/`, and `.env.example` files as well as the application source.

## 3. Configure secrets on the VM

```bash
cp .env.example .env
chmod 600 .env
openssl rand -base64 48
```

Edit `.env` on the VM. Use independent random values for `POSTGRES_PASSWORD`, `JWT_SIGNING_KEY`, and `INTERNAL_API_KEY`; the JWT key must be at least 32 characters. Set the SMTP sender, username, and password to a working SMTP account/app password. Never commit or share `.env`.

## 4. Build and start

```bash
sudo docker compose up -d --build
sudo docker compose ps
sudo docker compose logs --tail=100
```

The database initialization script runs only when PostgreSQL creates a new data volume. If a previous failed run left a volume behind, inspect it before deciding whether it can be deleted; deleting it removes database data. Do not run `docker compose down -v` on a VM with data you need.

## 5. Check the service

From the VM, verify the gateway health response:

```bash
curl -i http://127.0.0.1:8080/health
```

From your computer, open `http://<VM-public-IP>:8080/health`. The API routes are under `/api/auth`, `/api/users`, `/api/notes`, and `/api/labels`. Swagger is enabled only in Development, so it is not exposed by this Production setup.

## Current project-specific notes

- PostgreSQL is the only database used by this source; the guide's Redis steps do not match this checkout.
- The User API stores sessions in `UserSessions`; the supplied init SQL creates that table and its indexes for fresh databases. PostgreSQL skips init scripts when the data volume already exists. Apply the session schema once to an existing volume with `sudo docker compose exec -T postgres sh -c 'psql -U "$POSTGRES_USER" -d fundoo_users' < deploy/postgres-init/02-user-sessions.sql`. The User API does not apply EF migrations automatically. The Notes API has no EF migrations; the supplied init SQL creates the tables its repositories query.
- The Notes API calls the User API to validate the backing session for each authenticated request. Keep `UserApi__BaseUrl` set to the private User API address; if session validation is unavailable, Notes API returns `503` rather than accepting an unverified session.
- The live VM uses Docker Compose; K3s and Jenkins are not part of this deployment. Keep PostgreSQL's volume backed up.
- The VM's automatic deploy watcher is configured independently of the repository files and checks GitHub `main` every minute.

## Automatic deployment from GitHub

The Azure VM checks `origin/main` once per minute with a systemd timer. When it finds a new commit, it fetches the code, rebuilds the Compose images, restarts the services, and checks `/health`. If the build or health check fails, it restores the previous commit and rebuilds that version. The watcher refuses to deploy if the VM checkout has uncommitted changes. The VM's `.env` file and PostgreSQL volume remain outside Git.

Install `deploy/auto-deploy.sh` as `/usr/local/sbin/fundoo-auto-deploy`, install the service and timer files under `/etc/systemd/system/`, then run `sudo systemctl daemon-reload && sudo systemctl enable --now fundoo-auto-deploy.timer`. Check runs with `sudo journalctl -u fundoo-auto-deploy.service` and timer state with `systemctl list-timers fundoo-auto-deploy.timer`.
