# Stage 1: Build & Publish the application
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build-env
WORKDIR /src

# Copy everything from your repository root into /src
COPY . .

# Since Momentum.csproj is in the root, point directly to it without a folder prefix
RUN dotnet restore "Momentum.csproj"
RUN dotnet publish "Momentum.csproj" -c Release -o /app/publish

# Stage 2: Build the lightweight runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview
WORKDIR /app
COPY --from=build-env /app/publish .

# Render environment variable mapping fallback
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "Momentum.dll"]