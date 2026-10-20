FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore ApiEmprestimos.slnx
RUN dotnet publish src/ApiEmprestimos.Api/ApiEmprestimos.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .
USER $APP_UID

ENTRYPOINT ["dotnet", "ApiEmprestimos.Api.dll"]
