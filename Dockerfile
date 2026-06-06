FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia NuGet.Config limpo primeiro
COPY ["NuGet.Config", "."]

COPY ["Sinchrony.Api/Sinchrony.Api.csproj", "Sinchrony.Api/"]
COPY ["Sinchrony.Application/Sinchrony.Application.csproj", "Sinchrony.Application/"]
COPY ["Sinchrony.Domain/Sinchrony.Domain.csproj", "Sinchrony.Domain/"]
COPY ["Sinchrony.Infrastructure/Sinchrony.Infrastructure.csproj", "Sinchrony.Infrastructure/"]

RUN dotnet restore "Sinchrony.Api/Sinchrony.Api.csproj"

COPY . .

RUN dotnet publish "Sinchrony.Api/Sinchrony.Api.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Sinchrony.Api.dll"]