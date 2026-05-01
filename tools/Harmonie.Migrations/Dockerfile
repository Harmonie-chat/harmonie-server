# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Directory.Packages.props", "./"]
COPY ["tools/Harmonie.Migrations/Harmonie.Migrations.csproj", "tools/Harmonie.Migrations/"]
RUN dotnet restore "tools/Harmonie.Migrations/Harmonie.Migrations.csproj"

COPY tools/Harmonie.Migrations/ tools/Harmonie.Migrations/
WORKDIR "/src/tools/Harmonie.Migrations"
RUN dotnet publish "Harmonie.Migrations.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Harmonie.Migrations.dll"]
