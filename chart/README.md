# Local Minikube deployment

This chart deploys two Elsa server replicas into Minikube. It reuses the
PostgreSQL container from `docker-compose.yaml` through
`host.minikube.internal`; PostgreSQL is not installed by the chart.

This is an unauthenticated, local-only evidence environment. Compose publishes
PostgreSQL on the host with development credentials, so use it only on a trusted
network or override the credentials in both Compose and `values.yaml`.

## Prerequisites

- Docker Desktop
- Minikube with the `ingress` addon enabled
- Helm 4
- kubectl

Start PostgreSQL from the repository root:

```shell
docker compose up -d postgres
docker compose ps postgres
```

## Build and load the image

```shell
docker build \
  --file src/Elsa.ProtoActor.Repro.Server/Dockerfile \
  --tag elsa-protoactor-repro:local \
  .

minikube image load --overwrite=true elsa-protoactor-repro:local
```

The chart uses `imagePullPolicy: Never`, so Kubernetes will use this node-local
image and will not try to pull it from a registry.

## Install

```shell
helm upgrade --install elsa-protoactor-repro ./chart \
  --namespace elsa-protoactor-repro \
  --create-namespace \
  --wait \
  --timeout 10m
```

Check the deployment and Proto.Actor RBAC:

```shell
kubectl --namespace elsa-protoactor-repro get pods,service,ingress
kubectl --namespace elsa-protoactor-repro rollout status \
  deployment/elsa-protoactor-repro
kubectl auth can-i patch pods \
  --as=system:serviceaccount:elsa-protoactor-repro:elsa-protoactor-repro \
  --namespace elsa-protoactor-repro
```

## Open the ingress on macOS

Keep the tunnel running in another terminal:

```shell
minikube tunnel --bind-address=127.0.0.1
```

Then open <http://elsa-protoactor-repro.localhost>. The ingress uses cookie
affinity so the server-side Blazor dashboard remains on one replica.

Verify the probes through ingress:

```shell
curl http://elsa-protoactor-repro.localhost/utils/hcheck/liveness
curl http://elsa-protoactor-repro.localhost/utils/hcheck/readiness
curl http://elsa-protoactor-repro.localhost/utils/hcheck/startup
```

## Troubleshooting

- `ErrImageNeverPull`: rebuild the exact image tag shown above and rerun
  `minikube image load --overwrite=true elsa-protoactor-repro:local`.
- PostgreSQL connection failures: confirm `docker compose ps postgres` is
  healthy, then test `host.minikube.internal:5432` from a temporary pod.
- Startup probe failures: inspect the pod logs. The probe allows five minutes
  for database migrations and cluster startup.
- Ingress connection failures on macOS: keep `minikube tunnel` running and
  verify that the nginx ingress controller is ready.
