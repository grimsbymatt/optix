# syntax=docker/dockerfile:1

# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so the package layer is cached until a project file changes.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Movies.Core/Movies.Core.csproj src/Movies.Core/
COPY src/Movies.Infrastructure/Movies.Infrastructure.csproj src/Movies.Infrastructure/
COPY src/Movies.Api/Movies.Api.csproj src/Movies.Api/
RUN dotnet restore src/Movies.Api/Movies.Api.csproj

COPY resources/ resources/
COPY src/ src/
RUN dotnet publish src/Movies.Api/Movies.Api.csproj -c Release -o /app --no-restore

# ---- runtime ----
# The Ubuntu-based image includes ICU, which the accent-insensitive search relies on
# (Alpine/chiseled images default to globalization-invariant mode).
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Movies.Api.dll"]
