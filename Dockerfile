# ---------- build stage ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# копируем sln и csproj
COPY RevisorBot.sln ./
COPY Revisor.Bot/Revisor.Bot.csproj Revisor.Bot/

RUN dotnet restore

# копируем остальной код
COPY . .
WORKDIR /src/Revisor.Bot

RUN dotnet publish -c Release -o /app/publish

# ---------- runtime stage ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y postgresql-client \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Revisor.Bot.dll"]
