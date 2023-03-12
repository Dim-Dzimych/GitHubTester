FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["TelegramEarningBot.Api/TelegramEarningBot.Api.csproj", "TelegramEarningBot.Api/"]
RUN dotnet restore "TelegramEarningBot.Api/TelegramEarningBot.Api.csproj"
COPY . .
WORKDIR "/src/TelegramEarningBot.Api"
RUN dotnet build "TelegramEarningBot.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TelegramEarningBot.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TelegramEarningBot.Api.dll"]
