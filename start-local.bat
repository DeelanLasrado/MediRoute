@echo off
REM MediRoute local launch helper (Windows)
REM Requires: SQL Server / Redis / RabbitMQ running (or: docker compose up sql-server redis rabbitmq -d)

start "MediRoute-Hospital" cmd /k "dotnet run --project src\MediRoute.HospitalService --launch-profile http"
timeout /t 5 /nobreak >nul
start "MediRoute-Triage" cmd /k "dotnet run --project src\MediRoute.TriageService --launch-profile http"
start "MediRoute-Routing" cmd /k "dotnet run --project src\MediRoute.RoutingService --launch-profile http"
start "MediRoute-Notification" cmd /k "dotnet run --project src\MediRoute.NotificationService --launch-profile http"
timeout /t 3 /nobreak >nul
start "MediRoute-Gateway" cmd /k "dotnet run --project src\MediRoute.ApiGateway --launch-profile http"
start "MediRoute-Web" cmd /k "dotnet run --project src\MediRoute.Web"

echo MediRoute services starting...
echo Gateway: http://localhost:5000
echo Web UI:  check the MediRoute-Web window for the port (usually https://localhost:7xxx)
