# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props StockDesk.sln ./
COPY src/StockDesk.Domain/*.csproj src/StockDesk.Domain/
COPY src/StockDesk.Application/*.csproj src/StockDesk.Application/
COPY src/StockDesk.Infrastructure/*.csproj src/StockDesk.Infrastructure/
COPY src/StockDesk.Web/*.csproj src/StockDesk.Web/
RUN dotnet restore src/StockDesk.Web/StockDesk.Web.csproj
COPY src/ src/
RUN dotnet publish src/StockDesk.Web/StockDesk.Web.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "StockDesk.Web.dll"]
