# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["Directory.Packages.props", "./"]
COPY ["Harmonie.sln", "./"]
COPY ["src/Harmonie.Domain/Harmonie.Domain.csproj", "src/Harmonie.Domain/"]
COPY ["src/Harmonie.Application/Harmonie.Application.csproj", "src/Harmonie.Application/"]
COPY ["src/Harmonie.Infrastructure/Harmonie.Infrastructure.csproj", "src/Harmonie.Infrastructure/"]
COPY ["src/Harmonie.API/Harmonie.API.csproj", "src/Harmonie.API/"]

# Restore dependencies
RUN dotnet restore "src/Harmonie.API/Harmonie.API.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/src/Harmonie.API"
RUN dotnet build "Harmonie.API.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "Harmonie.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Copy published app
COPY --from=publish /app/publish .

# Set environment
ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_ENVIRONMENT=Production

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:80/health || exit 1

ENTRYPOINT ["dotnet", "Harmonie.API.dll"]
