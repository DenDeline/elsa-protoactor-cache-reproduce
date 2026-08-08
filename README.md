# Elsa Proto.Actor cache reproducer

This is a minimal, package-only Elsa host for investigating the distributed-cache
subscription behavior in `Elsa.Caching.Distributed.ProtoActor` 3.7.1. It contains:

- Elsa workflow management and runtime persistence backed by PostgreSQL.
- The Proto.Actor workflow runtime and Proto.Actor distributed cache transport.
- The Proto.Actor cluster dashboard hosted at `/`.
- Unsecured Elsa workflow API endpoints under the package's default route prefix.

## Build

Start the PostgreSQL database:

```shell
docker compose up -d
```

The Compose service uses the same `elsa` database, username, password, and port as
the development connection string.

```shell
dotnet build Elsa.ProtoActor.Repro.sln
```

The connection string in `appsettings.json` is deliberately a local stub, and a build
does not connect to PostgreSQL. Automatic migrations are disabled for this stage so
the host will not mutate a database implicitly. Before running it, replace the stub
and provision the Elsa schema:

```shell
dotnet run --project src/Elsa.ProtoActor.Repro.Server
```

The HTTP launch profile serves the dashboard at `http://localhost:5080`.

## Stage-two seam

The host reads `ConnectionStrings:elsa`, which a later Aspire AppHost can inject by
referencing a PostgreSQL database resource named `elsa`. Aspire is intentionally not
implemented here.

The current host also deliberately retains Elsa's default in-process Proto.Actor test
cluster provider. A two-member reproducer will need to replace that provider and the
localhost-only remote binding with shared discovery and per-member advertised addresses.
For production-faithful Pub/Sub lifecycle coverage, it should also replace the default
process-local subscriber store with shared durable storage (production uses Redis).
Proto.Actor event/snapshot persistence remains in memory; PostgreSQL is used for Elsa's
workflow management and runtime stores.
