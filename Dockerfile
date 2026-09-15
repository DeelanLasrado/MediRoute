# Multi-stage Dockerfile for MediRoute API Gateway (entry point for Azure App Service)
# Build context: repository root

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY MediRoute.sln ./
COPY src/MediRoute.Shared/MediRoute.Shared.csproj src/MediRoute.Shared/
COPY src/MediRoute.ApiGateway/MediRoute.ApiGateway.csproj src/MediRoute.ApiGateway/
COPY src/MediRoute.TriageService/MediRoute.TriageService.csproj src/MediRoute.TriageService/
COPY src/MediRoute.HospitalService/MediRoute.HospitalService.csproj src/MediRoute.HospitalService/
COPY src/MediRoute.RoutingService/MediRoute.RoutingService.csproj src/MediRoute.RoutingService/
COPY src/MediRoute.NotificationService/MediRoute.NotificationService.csproj src/MediRoute.NotificationService/

RUN dotnet restore src/MediRoute.ApiGateway/MediRoute.ApiGateway.csproj
RUN dotnet restore src/MediRoute.HospitalService/MediRoute.HospitalService.csproj
RUN dotnet restore src/MediRoute.TriageService/MediRoute.TriageService.csproj
RUN dotnet restore src/MediRoute.RoutingService/MediRoute.RoutingService.csproj
RUN dotnet restore src/MediRoute.NotificationService/MediRoute.NotificationService.csproj

COPY src/ ./src/

RUN dotnet publish src/MediRoute.ApiGateway/MediRoute.ApiGateway.csproj -c Release -o /app/gateway --no-restore
RUN dotnet publish src/MediRoute.HospitalService/MediRoute.HospitalService.csproj -c Release -o /app/hospital --no-restore
RUN dotnet publish src/MediRoute.TriageService/MediRoute.TriageService.csproj -c Release -o /app/triage --no-restore
RUN dotnet publish src/MediRoute.RoutingService/MediRoute.RoutingService.csproj -c Release -o /app/routing --no-restore
RUN dotnet publish src/MediRoute.NotificationService/MediRoute.NotificationService.csproj -c Release -o /app/notification --no-restore

# --- Gateway runtime (default image) ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS gateway
WORKDIR /app
COPY --from=build /app/gateway .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "MediRoute.ApiGateway.dll"]

# --- Hospital Service ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS hospital
WORKDIR /app
COPY --from=build /app/hospital .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "MediRoute.HospitalService.dll"]

# --- Triage Service ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS triage
WORKDIR /app
COPY --from=build /app/triage .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "MediRoute.TriageService.dll"]

# --- Routing Service ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS routing
WORKDIR /app
COPY --from=build /app/routing .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "MediRoute.RoutingService.dll"]

# --- Notification Service ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS notification
WORKDIR /app
COPY --from=build /app/notification .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "MediRoute.NotificationService.dll"]
