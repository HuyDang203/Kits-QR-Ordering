# Build: docker build -t annap-web .
# Run with compose: docker compose -f docker-compose.prod.yml up -d
FROM node:20-alpine AS css
WORKDIR /src
COPY Annap.CoffeeQrOrdering.Web/package.json Annap.CoffeeQrOrdering.Web/package-lock.json ./Annap.CoffeeQrOrdering.Web/
RUN cd Annap.CoffeeQrOrdering.Web && npm ci
COPY Annap.CoffeeQrOrdering.Web/ ./Annap.CoffeeQrOrdering.Web/
RUN cd Annap.CoffeeQrOrdering.Web && npm run build:css

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Annap.CoffeeQrOrdering.Domain/Annap.CoffeeQrOrdering.Domain.csproj Annap.CoffeeQrOrdering.Domain/
COPY Annap.CoffeeQrOrdering.Application/Annap.CoffeeQrOrdering.Application.csproj Annap.CoffeeQrOrdering.Application/
COPY Annap.CoffeeQrOrdering.Infrastructure/Annap.CoffeeQrOrdering.Infrastructure.csproj Annap.CoffeeQrOrdering.Infrastructure/
COPY Annap.CoffeeQrOrdering.Web/Annap.CoffeeQrOrdering.Web.csproj Annap.CoffeeQrOrdering.Web/
RUN dotnet restore Annap.CoffeeQrOrdering.Web/Annap.CoffeeQrOrdering.Web.csproj
COPY Annap.CoffeeQrOrdering.Domain/ Annap.CoffeeQrOrdering.Domain/
COPY Annap.CoffeeQrOrdering.Application/ Annap.CoffeeQrOrdering.Application/
COPY Annap.CoffeeQrOrdering.Infrastructure/ Annap.CoffeeQrOrdering.Infrastructure/
COPY Annap.CoffeeQrOrdering.Web/ Annap.CoffeeQrOrdering.Web/
COPY --from=css /src/Annap.CoffeeQrOrdering.Web/wwwroot/css/site.css Annap.CoffeeQrOrdering.Web/wwwroot/css/site.css
RUN dotnet publish Annap.CoffeeQrOrdering.Web/Annap.CoffeeQrOrdering.Web.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
COPY docs/ /docs/
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=40s --retries=3 \
    CMD curl -fsS "http://localhost:${PORT:-8080}/health" || exit 1
ENTRYPOINT ["dotnet", "Annap.CoffeeQrOrdering.Web.dll"]
