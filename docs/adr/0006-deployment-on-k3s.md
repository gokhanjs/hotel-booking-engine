# ADR 0006: Deployment on k3s

**Status:** Accepted

## Context

The service should deploy the way a small SaaS product would: repeatable, zero downtime, horizontally scalable, and portable to managed infrastructure later. It runs on a single VPS today.

## Decision

- **k3s** (certified Kubernetes) with **Kustomize** manifests. Rolling updates with `maxUnavailable: 0`, startup/liveness/readiness probes, a `preStop` delay, a PodDisruptionBudget and a CPU-based HorizontalPodAutoscaler.
- **One image, two entry points.** The API image also contains the EF Core migration bundle. Migrations run as a Kubernetes Job before the rollout, so schema and application versions cannot diverge.
- **Hardened runtime:** chiseled (distroless) ASP.NET image with ICU and tzdata, non-root user, read-only root filesystem, all capabilities dropped, network policies that only allow API to data traffic and ingress to API traffic.
- **Push-based delivery over SSH.** CI renders manifests with the image pinned by digest, copies them to the server and runs an ordered deploy script (secrets, data services, migration Job, rollout, automatic `rollout undo` on failure). The Kubernetes API is never exposed to the internet.
- **Data services in the cluster.** PostgreSQL runs as a StatefulSet with a persistent volume; Redis runs without persistence as a pure cache.

## Consequences

- The same manifests run on EKS, GKE or AKS; only the ingress class and storage class would change.
- The deploy procedure was verified on a local k3s cluster, including a failed release that rolled back automatically while 69 of 69 probe requests kept returning `200`.
- In-cluster PostgreSQL is a single point of failure and backups are manual (see the runbook). A production SaaS would use a managed database; switching only changes the connection string secret.
- Secrets are created from GitHub environment secrets at deploy time. Sealed Secrets or an external secrets operator would allow them to be managed declaratively.

## Alternatives considered

- **Docker Compose on the VPS:** simpler, but no rolling updates, self-healing or horizontal scaling without reimplementing them.
- **Pull-based GitOps (Argo CD, Flux):** the better model for multiple environments and drift detection; it adds a controller to operate and is the natural next step.
- **Helm chart:** templating is unnecessary for one environment; Kustomize patches keep manifests plain YAML.
- **Running migrations on application startup:** races between replicas and ties schema changes to pod restarts. Running them as a Job also makes it possible to give the application a database user without DDL rights; today both still share one user.
