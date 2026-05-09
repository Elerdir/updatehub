FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/UpdateHub.Web/UpdateHub.Web.csproj UpdateHub.Web/
RUN dotnet restore UpdateHub.Web/UpdateHub.Web.csproj
COPY src/UpdateHub.Web/ UpdateHub.Web/
RUN dotnet publish UpdateHub.Web/UpdateHub.Web.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Data directories are mounted as volumes
RUN mkdir -p /app/data/artifacts

ENV ASPNETCORE_ENVIRONMENT=Production
ENV UpdateHub__DatabasePath=/app/data/updatehub.db
ENV UpdateHub__StoragePath=/app/data/artifacts

EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "UpdateHub.Web.dll"]
