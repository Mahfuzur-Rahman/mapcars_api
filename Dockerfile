# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Mapcars.sln ./
COPY src/Mapcars.Api/Mapcars.Api.csproj src/Mapcars.Api/
COPY src/Mapcars.Application/Mapcars.Application.csproj src/Mapcars.Application/
COPY src/Mapcars.Domain/Mapcars.Domain.csproj src/Mapcars.Domain/
COPY src/Mapcars.Infrastructure/Mapcars.Infrastructure.csproj src/Mapcars.Infrastructure/
RUN dotnet restore Mapcars.sln

COPY src/ src/
RUN dotnet publish src/Mapcars.Api/Mapcars.Api.csproj -c Release -o /app/publish --no-restore

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Mapcars.Api.dll"]
