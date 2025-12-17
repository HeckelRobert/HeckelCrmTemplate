FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["HeckelCrm.Web/HeckelCrm.Web.csproj", "HeckelCrm.Web/"]
COPY ["HeckelCrm.Core/HeckelCrm.Core.csproj", "HeckelCrm.Core/"]
RUN dotnet restore "HeckelCrm.Web/HeckelCrm.Web.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/HeckelCrm.Web"
RUN dotnet build "HeckelCrm.Web.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "HeckelCrm.Web.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "HeckelCrm.Web.dll"]

