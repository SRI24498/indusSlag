# ---- Build Stage ----
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src

# Copy everything
COPY . .

# Restore & Build
RUN dotnet build NopCommerce.sln --no-incremental -c Release

# Publish Web App
WORKDIR /src/Presentation/Nop.Web
RUN dotnet publish Nop.Web.csproj -c Release -o /app/published --no-restore

# ---- Runtime Stage ----
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS runtime
WORKDIR /app
COPY --from=build /app/published .

# Create required folders
RUN mkdir -p logs bin
RUN chmod 775 App_Data App_Data/DataProtectionKeys bin logs

# Expose port
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Entry point
ENTRYPOINT ["dotnet", "Nop.Web.dll"]