# Brokerage

Brokerage is a .NET 10 modular-monolith starter backed by PostgreSQL. The repository includes a Docker Compose development environment and a React/Vite web client.

For application architecture, development, build, and API details, see [application/README.md](application/README.md).

## Requirements

- Docker Engine with the Compose plugin
- Git

To run the application directly on your machine instead of in Docker, install the .NET 10 SDK and see the application README.

## Start the development environment

From the repository root, create a local environment file:

```bash
cp .env.example .env
```

Edit `.env` and replace the placeholder passwords. Add a JWT signing key of at least 32 bytes:

```dotenv
Identity__JwtSigningKey=replace-with-at-least-32-bytes-of-random-secret
```

PgBouncer also reads `pgbouncer/userlist.txt`; ensure it contains the PostgreSQL user and password configured in `.env`.

Start the services:

```bash
docker compose up -d
```

Compose starts PostgreSQL, PgBouncer, pgAdmin, Traefik, and a .NET SDK container. The .NET container is intentionally left idle; start the Host inside it:

```bash
docker compose exec dotnet-app dotnet run \
  --project Brokerage.Host/Brokerage.Host.csproj \
  --urls http://0.0.0.0:8080
```

The application source is mounted at `/app` in the container. Once the Host is running, Traefik routes HTTP requests through `http://localhost`.

## Service addresses

- Brokerage Host: `http://localhost` (after starting it in the .NET container)
- pgAdmin: `http://localhost/pgadmin`
- Traefik dashboard: `http://localhost:8080/dashboard/`

PostgreSQL and PgBouncer are available to other Compose services on the internal network; their ports are not published directly to the host. The Host's database connection is configured by `ConnectionStrings__DefaultConnection` in `.env`.

## Stop the environment

Stop the Compose services while retaining database and pgAdmin data:

```bash
docker compose down
```

The database and pgAdmin data are stored in the `postgres_data/` and `pgadmin_data/` directories.

## Security

This Compose setup is for local development, not production. Change all example credentials, keep `.env` and `pgbouncer/userlist.txt` out of source control, and do not expose the Traefik dashboard or services publicly without securing them.
