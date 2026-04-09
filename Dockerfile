FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Telegram_bot.csproj ./
RUN dotnet restore Telegram_bot.csproj

COPY . ./
RUN dotnet publish Telegram_bot.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

COPY --from=build /app/publish ./


ENTRYPOINT ["dotnet", "Telegram_bot.dll"]
