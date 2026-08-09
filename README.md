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

The development connection string in `appsettings.json` points to the Compose database,
and a build does not connect to PostgreSQL. When the host starts, Elsa applies its EF
Core migrations to that database automatically.

```shell
dotnet run --project src/Elsa.ProtoActor.Repro.Server
```

The HTTP launch profile serves the dashboard at `http://localhost:5080`.

## Kubernetes cluster mode

Outside Kubernetes, the host keeps Elsa's default in-process test cluster provider and
binds Proto.Remote to localhost. When `KUBERNETES_SERVICE_HOST` is present, it switches
to `KubernetesProvider` and binds Proto.Remote to all interfaces using the pod IP as its
advertised address.

Supply the advertised address through the Kubernetes Downward API:

```yaml
env:
  - name: ProtoActor__AdvertisedHost
    valueFrom:
      fieldRef:
        fieldPath: status.podIP

livenessProbe:
  httpGet:
    path: /utils/hcheck/liveness
    port: 8080
readinessProbe:
  httpGet:
    path: /utils/hcheck/readiness
    port: 8080
startupProbe:
  httpGet:
    path: /utils/hcheck/startup
    port: 8080
```

The service account needs `get`, `list`, `watch`, and `patch` on Pods in its namespace.
Keep the default service-account token, CA, and namespace mounts enabled, and do not
override the pod hostname because the provider uses it as the pod resource name. The
cluster name becomes a Kubernetes label value, so it must be label-safe and no longer
than 63 characters. Pods must also permit direct pod-to-pod TCP traffic for the
dynamically selected Proto.Remote port; no Kubernetes Service is required for discovery.

The liveness probe fails only after the actor system shuts down. Readiness and startup
return HTTP 503 until the member joins its cluster, and while it is stopping. They do not
test PostgreSQL, Kubernetes watch health, peer count, or remote reachability.

## Later integration seam

The host reads `ConnectionStrings:Default`, which a later Aspire AppHost can inject.
Aspire is intentionally not implemented here. For production-faithful Pub/Sub lifecycle
coverage, the host should also replace Proto.Actor's process-local subscriber store with
shared durable storage (production uses Redis). Proto.Actor event/snapshot persistence
remains in memory; PostgreSQL is used for Elsa's workflow management and runtime stores.
