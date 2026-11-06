FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY backend/Backend/Secretaria.Api/Secretaria.Api.csproj backend/Backend/Secretaria.Api/

RUN dotnet restore backend/Backend/Secretaria.Api/Secretaria.Api.csproj

COPY backend/Backend/Secretaria.Api/ backend/Backend/Secretaria.Api/

RUN dotnet publish backend/Backend/Secretaria.Api/Secretaria.Api.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0

WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://0.0.0.0:10000

EXPOSE 10000

ENTRYPOINT ["dotnet", "Secretaria.Api.dll"]
