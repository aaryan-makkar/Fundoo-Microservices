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

The deployment files are currently local and uncommitted. Once the intended project changes are committed to your Git remote, on the VM run:

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
- The User API has an EF migration, but the Notes API has no migrations. The supplied init SQL creates the tables the Notes repositories query and the initial User schema. Review schema changes against the application before reusing this for upgrades.
- This Compose setup is a first deploy path, not the guide's K3s/Jenkins CI/CD endpoint. Keep PostgreSQL's volume backed up. Jenkins and K3s can be introduced after the first working deployment.
- Deployment has not been run from this workspace because the VM is a remote Azure resource and no SSH session or registry credentials were provided.
