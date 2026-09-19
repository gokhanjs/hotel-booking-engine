#!/bin/sh
# Applies rendered manifests in dependency order: secrets, data services, migrations, then the API rollout.
set -eu

NS=booking-engine
DIR=$(cd "$(dirname "$0")" && pwd)
: "${POSTGRES_PASSWORD:?POSTGRES_PASSWORD is required}"
: "${MANAGEMENT_API_KEY:?MANAGEMENT_API_KEY is required}"

apply_secret() {
  kubectl -n "$NS" create secret generic "$@" --dry-run=client -o yaml | kubectl apply -f -
}

wait_for_job() {
  for _ in $(seq 1 60); do
    conditions=$(kubectl -n "$NS" get job "$1" -o jsonpath='{.status.conditions[?(@.status=="True")].type}')
    case "$conditions" in
      *Complete*) return 0 ;;
      *Failed*) break ;;
    esac
    sleep 5
  done
  kubectl -n "$NS" logs "job/$1" --tail=100 || true
  return 1
}

kubectl create namespace "$NS" --dry-run=client -o yaml | kubectl apply -f -
apply_secret postgres-credentials --from-literal=POSTGRES_PASSWORD="$POSTGRES_PASSWORD"
apply_secret api-secrets \
  --from-literal=ConnectionStrings__Postgres="Host=postgres;Port=5432;Database=booking;Username=booking;Password=$POSTGRES_PASSWORD;Gss Encryption Mode=Disable" \
  --from-literal=Management__ApiKey="$MANAGEMENT_API_KEY"

kubectl apply -f "$DIR/data.yaml"
kubectl -n "$NS" rollout status statefulset/postgres --timeout=180s
kubectl -n "$NS" rollout status deployment/redis --timeout=120s

kubectl -n "$NS" delete job booking-engine-migrate --ignore-not-found --wait=true
kubectl apply -f "$DIR/migrate.yaml"
wait_for_job booking-engine-migrate

kubectl apply -f "$DIR/app.yaml"
if ! kubectl -n "$NS" rollout status deployment/booking-engine-api --timeout=300s; then
  kubectl -n "$NS" rollout undo deployment/booking-engine-api
  exit 1
fi
