# Simple .NET 10 API Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["AI.ProfilePhotoMaker.API.csproj", "."]
RUN dotnet restore "AI.ProfilePhotoMaker.API.csproj"
COPY . .
RUN dotnet build "AI.ProfilePhotoMaker.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AI.ProfilePhotoMaker.API.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:8080/api/health || exit 1

ENTRYPOINT ["dotnet", "AI.ProfilePhotoMaker.API.dll"]
