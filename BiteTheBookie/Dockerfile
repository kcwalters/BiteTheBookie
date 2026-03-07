# Base runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# 👇 Add your required environment variables here
# These can be overridden at runtime (recommended)
ENV OddsApi__ApiKey="4e7e049b298c50d9b16021b5112f67dd"
ENV AzureOpenAI__Endpoint=""
ENV AzureOpenAI__ApiKey=""
ENV AzureOpenAI__Deployment=""
ENV AzureOpenAI__Model="gpt-4o-mini"

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
