# Standard glibc (Debian/Ubuntu) runtime. Do not switch to *-alpine (musl);
# musl-based .NET images have known native compatibility issues that can SIGSEGV.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER root
RUN apt-get update && apt-get install -y --no-install-recommends libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
EXPOSE 8080
# Workstation GC (also set in the Api csproj). Server GC over-allocates on 512MB instances.
ENV DOTNET_gcServer=0
USER $APP_UID

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
