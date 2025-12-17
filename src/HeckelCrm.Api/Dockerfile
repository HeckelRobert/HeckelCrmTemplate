FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["HeckelCrm.Api/HeckelCrm.Api.csproj", "HeckelCrm.Api/"]
COPY ["HeckelCrm.Core/HeckelCrm.Core.csproj", "HeckelCrm.Core/"]
COPY ["HeckelCrm.Infrastructure/HeckelCrm.Infrastructure.csproj", "HeckelCrm.Infrastructure/"]
RUN dotnet restore "HeckelCrm.Api/HeckelCrm.Api.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/HeckelCrm.Api"
RUN dotnet build "HeckelCrm.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "HeckelCrm.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "HeckelCrm.Api.dll"]

