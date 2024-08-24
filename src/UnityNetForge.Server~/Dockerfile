FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["UnityNetForge.Api/UnityNetForge.Api.csproj", "UnityNetForge.Api/"]
RUN dotnet restore "UnityNetForge.Api/UnityNetForge.Api.csproj"
COPY . .
WORKDIR "/src/UnityNetForge.Api"
RUN dotnet build "UnityNetForge.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "UnityNetForge.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "UnityNetForge.Api.dll"]
