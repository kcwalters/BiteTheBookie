# Base runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Non-secret defaults only. Secrets (API keys, endpoints) should be
# injected at runtime via docker run -e, docker-compose, or Azure App Settings.
# Do NOT set them here as empty strings — they override appsettings.json.

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["BiteTheBookie/BiteTheBookie.csproj", "BiteTheBookie/"]
RUN dotnet restore "./BiteTheBookie/BiteTheBookie.csproj"
COPY . .
WORKDIR "/src/BiteTheBookie"
RUN dotnet build "./BiteTheBookie.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./BiteTheBookie.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final runtime image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BiteTheBookie.dll"]
