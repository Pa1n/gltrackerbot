# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish -c Release -o /app

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app .
# данные можно монтировать в Render как volume или хранить в репозитории
COPY data ./data
ENTRYPOINT ["dotnet", "MyBot.dll"]