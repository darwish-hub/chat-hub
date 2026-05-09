FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["ChatHub.sln", "./"]
COPY ["ChatHub.Core/ChatHub.Core.csproj", "ChatHub.Core/"]
COPY ["ChatHub.Infrastructure/ChatHub.Infrastructure.csproj", "ChatHub.Infrastructure/"]
COPY ["ChatHub.Api/ChatHub.Api.csproj", "ChatHub.Api/"]
RUN dotnet restore
COPY . .
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ChatHub.Api.dll"]
