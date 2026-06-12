FROM node:22-bookworm-slim AS node

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY --from=node /usr/local /usr/local
ENV PATH=/usr/local/bin:${PATH}
RUN node --version
COPY src/OrganizationHub.Web/OrganizationHub.Web.csproj src/OrganizationHub.Web/
RUN dotnet restore src/OrganizationHub.Web/OrganizationHub.Web.csproj
COPY . .
RUN dotnet publish src/OrganizationHub.Web/OrganizationHub.Web.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl ca-certificates \
    && rm -rf /var/lib/apt/lists/*
RUN mkdir -p /app/Data
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 CMD curl -fsS http://127.0.0.1:8080/healthz || exit 1
ENTRYPOINT ["dotnet", "OrganizationHub.Web.dll"]
