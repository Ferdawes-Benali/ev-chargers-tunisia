# EV Chargers Tunisia API. Build from the backend folder: docker build -t evchargers-api ./backend
# Configuration comes from environment variables only (see docker.env.example); no secrets in the image.

# ---- Build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Project files first: the restore layer is reused until a dependency changes
COPY src/EvChargers.Domain/EvChargers.Domain.csproj src/EvChargers.Domain/
COPY src/EvChargers.Application/EvChargers.Application.csproj src/EvChargers.Application/
COPY src/EvChargers.Infrastructure/EvChargers.Infrastructure.csproj src/EvChargers.Infrastructure/
COPY src/EvChargers.API/EvChargers.API.csproj src/EvChargers.API/
RUN dotnet restore src/EvChargers.API/EvChargers.API.csproj

COPY src/ src/
RUN dotnet publish src/EvChargers.API/EvChargers.API.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

# ---- Runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Render sends plain HTTP to this port and terminates TLS itself
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .

# Non-root user provided by the .NET base images
USER $APP_UID

ENTRYPOINT ["dotnet", "EvChargers.API.dll"]
