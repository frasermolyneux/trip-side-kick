FROM node:22-bookworm-slim AS frontend
WORKDIR /client
COPY src/MX.TripSideKick.Web/ClientApp/package*.json ./
RUN npm ci
COPY src/MX.TripSideKick.Web/ClientApp/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY . .
COPY --from=frontend /wwwroot/ ./src/MX.TripSideKick.Web/wwwroot/
RUN dotnet publish src/MX.TripSideKick.Web/MX.TripSideKick.Web.csproj \
    --configuration Release --output /publish /p:SkipClientBuild=true /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 ASPNETCORE_ENVIRONMENT=Production DOTNET_EnableDiagnostics=0 \
    TRIPSIDEKICK_RUNTIME_FILE=/run/secrets/runtime-configuration
COPY --from=build /publish/ ./
RUN mkdir -p /app/state/keys /app/state/documents && chown -R 1654:1654 /app/state
USER 1654:1654
EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=5s --start-period=30s --retries=3 \
    CMD ["dotnet", "MX.TripSideKick.Web.dll", "--readiness"]
ENTRYPOINT ["dotnet", "MX.TripSideKick.Web.dll"]
