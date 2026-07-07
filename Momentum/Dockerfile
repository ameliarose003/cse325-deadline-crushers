# Use the official .NET 10 Preview SDK image to build the app
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build-env
WORKDIR /App

# Copy everything and restore/publish
COPY . ./
RUN dotnet restore
RUN dotnet publish -c Release -o out

# Build runtime image using .NET 10 Preview
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview
WORKDIR /App
COPY --from=build-env /App/out .

# Render dynamically assigns a port via the PORT environment variable
ENV ASPNETCORE_URLS=http://+:10000
EXPOSE 10000

ENTRYPOINT ["dotnet", "Momentum.dll"]