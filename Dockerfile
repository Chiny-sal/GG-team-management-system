FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/GG.TeamManagement.Api/GG.TeamManagement.Api.csproj", "GG.TeamManagement.Api/"]
COPY ["src/GG.TeamManagement.Application/GG.TeamManagement.Application.csproj", "GG.TeamManagement.Application/"]
COPY ["src/GG.TeamManagement.Infrastructure/GG.TeamManagement.Infrastructure.csproj", "GG.TeamManagement.Infrastructure/"]
COPY ["src/GG.TeamManagement.Domain/GG.TeamManagement.Domain.csproj", "GG.TeamManagement.Domain/"]
RUN dotnet restore "./GG.TeamManagement.Api/GG.TeamManagement.Api.csproj"
COPY src/ .
WORKDIR "/src/GG.TeamManagement.Api"
RUN dotnet build "./GG.TeamManagement.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./GG.TeamManagement.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GG.TeamManagement.Api.dll"]
