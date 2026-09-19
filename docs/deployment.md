# Deployment runbook

This runbook takes a fresh Linux server to a running production deployment and covers day-2 operations. The pipeline itself is defined in [`.github/workflows/ci.yml`](../.github/workflows/ci.yml); the design is explained in [ADR 0006](adr/0006-deployment-on-k3s.md).

## 1. Server

- A VPS with at least 2 vCPU and 4 GB RAM, Ubuntu 24.04 LTS, `x86_64`. (The pipeline builds `linux/amd64`; for an ARM server change `platforms` in the workflow.)
- A DNS `A` record for the API host (for example `booking.example.com`) pointing to the server.
- Open ports: `22` (SSH), `80` and `443` (HTTP/HTTPS). Do **not** expose `6443` (Kubernetes API).

## 2. Install k3s

```bash
curl -sfL https://get.k3s.io | sh -
sudo k3s kubectl get nodes
```

k3s ships Traefik as the ingress controller, a local-path storage class, metrics-server (used by the autoscaler) and an embedded network policy controller.

The commands below reference files in `deploy/k8s/cluster/`; run them from a checkout of this repository or copy those two files to the server.

Apply the Traefik settings. They keep client IPs (needed for per-client rate limiting) and redirect HTTP to HTTPS:

```bash
sudo k3s kubectl apply -f deploy/k8s/cluster/traefik-config.yaml
```

## 3. Install cert-manager and the Let's Encrypt issuer

```bash
sudo k3s kubectl apply -f https://github.com/cert-manager/cert-manager/releases/download/v1.21.2/cert-manager.yaml
sudo k3s kubectl -n cert-manager rollout status deploy/cert-manager-webhook

export ACME_EMAIL=you@example.com
envsubst '${ACME_EMAIL}' < deploy/k8s/cluster/cluster-issuer.yaml | sudo k3s kubectl apply -f -
```

## 4. Deploy user

The pipeline connects over SSH and runs `kubectl` on the server.

```bash
sudo adduser --disabled-password --gecos "" deploy
sudo install -d -o deploy -g deploy -m 700 /home/deploy/.kube /home/deploy/.ssh
sudo install -o deploy -g deploy -m 600 /etc/rancher/k3s/k3s.yaml /home/deploy/.kube/config
# Add the public half of the CI key to /home/deploy/.ssh/authorized_keys
```

The copied kubeconfig has cluster-admin rights. A tighter setup binds a namespace-scoped Role to a dedicated ServiceAccount and gives the deploy user that token instead.

## 5. GitHub configuration

Create an environment named `production` (optionally with required reviewers) and add:

| Kind | Name | Value |
|---|---|---|
| Secret | `SSH_HOST` | server hostname or IP |
| Secret | `SSH_USER` | `deploy` |
| Secret | `SSH_PRIVATE_KEY` | private key whose public half is in `authorized_keys` |
| Secret | `SSH_KNOWN_HOSTS` | output of `ssh-keyscan <host>` |
| Secret | `POSTGRES_PASSWORD` | e.g. `openssl rand -hex 32` |
| Secret | `MANAGEMENT_API_KEY` | e.g. `openssl rand -hex 32` |
| Variable | `APP_HOST` | `booking.example.com` |

Then add the repository variable `DEPLOY_ENABLED=true`. Until it is set, the pipeline builds and publishes images but skips deployment.

Use hex or alphanumeric secrets: the deploy step passes them as `KEY=value` lines over SSH standard input.

The image is published to `ghcr.io/<owner>/<repo>`. Make the package public, or create an image pull secret in the `booking-engine` namespace and reference it from the Deployment and Job.

## 6. Deploy

Push to `main`. The `deploy` job:

1. renders `data.yaml`, `migrate.yaml` and `app.yaml` with the image pinned by digest (the rendered files are kept as a workflow artifact);
2. copies them with `deploy.sh` to `~/booking-engine-deploy` on the server;
3. runs `deploy.sh`, which creates or updates the secrets, applies PostgreSQL and Redis and waits for them, runs the migration Job, applies the API and waits for the rollout, and rolls back to the previous release if the new one does not become ready within five minutes (a failed first release has nothing to roll back to and simply fails the job);
4. calls `https://$APP_HOST/health/ready`.

## 7. Operations

```bash
alias k='kubectl -n booking-engine'

k get pods                                   # overview
k logs deploy/booking-engine-api -f          # API logs (any replica)
k logs job/booking-engine-migrate            # last migration run
k rollout history deploy/booking-engine-api
k rollout undo deploy/booking-engine-api     # manual rollback to the previous release
k get hpa booking-engine-api                 # autoscaler state
```

**Health endpoints.** `/health/live` only reports that the process responds and drives restarts. `/health/ready` checks PostgreSQL and Redis and gates traffic; a Redis outage reports `Degraded` and keeps the pod in rotation because the API falls back to the database.

**Backups.** PostgreSQL runs in the cluster without automated backups. Take a logical backup before risky changes and schedule one with cron:

```bash
kubectl -n booking-engine exec postgres-0 -- pg_dump -U booking -d booking -Fc > booking-$(date +%F).dump
```

Restore into an empty database with `pg_restore -U booking -d booking --clean`. For real customer data, move to a managed PostgreSQL service with point-in-time recovery and change only the connection string.

**Rotating secrets.** Secrets are read as environment variables, so a new `MANAGEMENT_API_KEY` takes effect when the API pods restart: a deploy of a new image does that, or run `k rollout restart deploy/booking-engine-api` after the secret is updated. `POSTGRES_PASSWORD` is only used by PostgreSQL when the data directory is first created; to rotate it, run `ALTER USER booking PASSWORD '...'` inside the database first, then update the GitHub secret and redeploy.

**Scaling.** The autoscaler keeps 2 to 5 API replicas at 70% CPU. On a single node this protects against pod failures and absorbs load spikes; for node failures add servers to the k3s cluster and move PostgreSQL to a managed service.

## 8. Troubleshooting

| Symptom | Likely cause |
|---|---|
| Migration Job fails with an extraction error | The Job needs `DOTNET_BUNDLE_EXTRACT_BASE_DIR=/tmp` because the root filesystem is read-only. |
| `libgssapi_krb5.so.2` in logs | The connection string is missing `Gss Encryption Mode=Disable`; `deploy.sh` sets it. |
| All storefront clients share one rate limit | Traefik is not preserving client IPs; check that `traefik-config.yaml` is applied (`externalTrafficPolicy: Local`). |
| Certificate stays pending | DNS does not point to the server yet, or port 80 is blocked (the HTTP-01 challenge needs it). |
| Pods stuck in `ImagePullBackOff` | The GHCR package is private and no pull secret is configured. |
