# MediRoute — AI-Powered Emergency Healthcare Routing System

**Live API (Azure Free):** https://mediroute-deelan.azurewebsites.net/swagger


Real-time hospital capacity tracking + AI triage routing for medical emergencies in India.

## Architecture

```
API Gateway (YARP :5000)
 ├── Triage Service     (:5001)  — Azure OpenAI GPT-4o symptom triage
 ├── Hospital Service   (:5002)  — Registry, capacity, auth, ambulances, Hangfire
 ├── Routing Service    (:5003)  — ML.NET ETA prediction + top-3 recommendations
 └── Notification Svc  (:5004)  — SignalR hubs + RabbitMQ capacity consumer

Blazor WASM Admin Dashboard  →  talks to Gateway
SQL Server · Redis · RabbitMQ
```

## Quick Start (Docker)

```bash
docker compose up --build
```

| Service        | URL                          |
|----------------|------------------------------|
| API Gateway    | http://localhost:5000        |
| Triage Swagger | http://localhost:5001/swagger|
| Hospital API   | http://localhost:5002/swagger|
| Routing API    | http://localhost:5003/swagger|
| Notifications  | http://localhost:5004/swagger|
| RabbitMQ UI    | http://localhost:15672 (guest/guest) |

Demo admin: `admin@mediroute.com` / `Admin@123`

## Local Development (without Docker)

```bash
# Start infra only
docker compose up sql-server redis rabbitmq -d

# Run services (separate terminals)
dotnet run --project src/MediRoute.HospitalService
dotnet run --project src/MediRoute.TriageService
dotnet run --project src/MediRoute.RoutingService
dotnet run --project src/MediRoute.NotificationService
dotnet run --project src/MediRoute.ApiGateway
dotnet run --project src/MediRoute.Web
```

## Key API Endpoints

| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/triage/analyze` | AI symptom triage + hospital routing |
| GET | `/api/hospitals` | List hospitals with capacity |
| PUT | `/api/hospitals/{id}/capacity` | Update beds/ICU/blood (auth) |
| GET | `/api/routing/recommend` | Top-3 hospitals by score |
| POST | `/api/ambulances/location` | GPS update (Redis + SignalR) |
| Hub | `/hubs/emergency` | Live capacity & ambulance events |

### Triage request example

```json
POST /api/triage/analyze
{
  "symptoms": "55yo male, severe chest pain radiating to left arm, sweating",
  "latitude": 28.6139,
  "longitude": 77.2090,
  "bloodTypeNeeded": "O+"
}
```

Returns `{ urgency, specialist_needed, estimated_stabilization_time }` plus top-3 hospitals ranked by `(availability × specialty) / predicted_ETA`.

## Azure Free / Cheap Tier Setup

| Resource | Tier | Notes |
|----------|------|-------|
| App Service | Free F1 or B1 (~$13/mo) | Host API |
| Azure SQL | Free 32GB serverless | Primary DB |
| Azure Cache for Redis | Basic C0 | Location cache |
| Azure OpenAI | Pay-per-token | GPT-4o triage |
| Azure SignalR | Free (20 connections) | Live updates |
| App Insights | Free tier | Monitoring |

1. Create resources in Azure Portal
2. Replace `CHANGE_ME` values in each service's `appsettings.json` (Azure connection strings included)
3. Add GitHub secrets:
   - `AZURE_WEBAPP_NAME`
   - `AZURE_WEBAPP_PUBLISH_PROFILE`
4. Push to `main` → `.github/workflows/deploy.yml` builds Docker images to GHCR and deploys

## Azure OpenAI

Set in Triage service (or `.env` for Docker):

```
AzureOpenAI__Endpoint=https://YOUR.openai.azure.com/
AzureOpenAI__ApiKey=...
AzureOpenAI__DeploymentName=gpt-4o
```

Without keys, triage falls back to a rule-based mock classifier (still demos the full flow).

## Tech Stack

ASP.NET Core 8 · YARP · SignalR · Azure OpenAI · ML.NET · EF Core · SQL Server · Redis · RabbitMQ · Hangfire · Identity + JWT · Blazor WASM · Docker · GitHub Actions · Application Insights
