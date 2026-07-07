# Stage 1: Build & Publish the application
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build-env
WORKDIR /src

# Copy everything
COPY . .

# Explicitly point to your .csproj file
# (Adjust "Momentum.csproj" to "Momentum/Momentum.csproj" if it's in a subfolder)
RUN dotnet restore "Momentum.csproj"
RUN dotnet publish "Momentum/Momentum.csproj" -c Release -o /app/publish

# Stage 2: Build the lightweight runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview
WORKDIR /app
COPY --from=build-env /app/publish .

# Render dynamically assigns a port via the PORT environment variable
ENV ASPNETCORE_URLS=http://+:10000
EXPOSE 10000

ENTRYPOINT ["dotnet", "Momentum.dll"]