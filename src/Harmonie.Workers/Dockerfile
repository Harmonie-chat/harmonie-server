# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["Directory.Packages.props", "./"]
COPY ["Harmonie.sln", "./"]
COPY ["src/Harmonie.Domain/Harmonie.Domain.csproj", "src/Harmonie.Domain/"]
COPY ["src/Harmonie.Application/Harmonie.Application.csproj", "src/Harmonie.Application/"]
COPY ["src/Harmonie.Infrastructure/Harmonie.Infrastructure.csproj", "src/Harmonie.Infrastructure/"]
COPY ["src/Harmonie.Workers/Harmonie.Workers.csproj", "src/Harmonie.Workers/"]

# Restore dependencies
RUN dotnet restore "src/Harmonie.Workers/Harmonie.Workers.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/src/Harmonie.Workers"
RUN dotnet build "Harmonie.Workers.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "Harmonie.Workers.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Copy published app
COPY --from=publish /app/publish .

# Set environment
ENV DOTNET_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "Harmonie.Workers.dll"]
