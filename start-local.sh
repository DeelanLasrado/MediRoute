#!/usr/bin/env bash
# MediRoute local launch helper
# Requires: SQL Server / Redis / RabbitMQ (or: docker compose up sql-server redis rabbitmq -d)

set -e
ROOT="$(cd "$(dirname "$0")" && pwd)"
cd "$ROOT"

export DOTNET_SKIP_WORKLOAD_INTEGRITY_CHECK=1

dotnet run --project src/MediRoute.HospitalService --launch-profile http &
sleep 4
dotnet run --project src/MediRoute.TriageService --launch-profile http &
dotnet run --project src/MediRoute.RoutingService --launch-profile http &
dotnet run --project src/MediRoute.NotificationService --launch-profile http &
sleep 3
dotnet run --project src/MediRoute.ApiGateway --launch-profile http &
dotnet run --project src/MediRoute.Web &

echo "MediRoute services starting..."
echo "Gateway: http://localhost:5000"
wait
