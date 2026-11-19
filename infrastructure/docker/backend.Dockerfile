# FinCore.Api y FinCore.Worker · imagen multi-etapa. Contexto de build: raíz del repositorio.
#   build    restaura y compila la solución (capa de restore cacheada por los .csproj)
#   test     ejecuta las pruebas (docker compose --profile test run --rm api-tests)
#   runtime  FinCore.Api (ASP.NET Core sin SDK, usuario no root, puerto 8080)
#   worker   FinCore.Worker (mismo runtime; en Windows el mismo binario se instala como servicio)

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY backend/nuget.config backend/Directory.Build.props backend/Directory.Packages.props backend/FinCore.slnx ./
COPY backend/FinCore.Domain/FinCore.Domain.csproj                 FinCore.Domain/
COPY backend/FinCore.Application/FinCore.Application.csproj       FinCore.Application/
COPY backend/FinCore.Infrastructure/FinCore.Infrastructure.csproj FinCore.Infrastructure/
COPY backend/FinCore.Api/FinCore.Api.csproj                       FinCore.Api/
COPY backend/FinCore.Worker/FinCore.Worker.csproj                 FinCore.Worker/
COPY backend/FinCore.Tests/FinCore.Tests.csproj                   FinCore.Tests/
RUN dotnet restore FinCore.slnx
# La consulta de Oracle compartida con SSIS se incrusta en FinCore.Infrastructure (ruta relativa ../../etl).
COPY etl/FinCore.Integration.SSIS/sql/ /etl/FinCore.Integration.SSIS/sql/
COPY backend/ .
RUN dotnet build FinCore.slnx -c Release --no-restore

FROM build AS test
ENTRYPOINT ["dotnet", "test", "FinCore.slnx", "-c", "Release", "--no-build", "--logger", "console;verbosity=normal"]

FROM build AS publish
RUN dotnet publish FinCore.Api/FinCore.Api.csproj -c Release --no-build -o /app/api \
 && dotnet publish FinCore.Worker/FinCore.Worker.csproj -c Release --no-build -o /app/worker

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=publish /app/api .
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_gcServer=0
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "FinCore.Api.dll"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS worker
WORKDIR /app
COPY --from=publish /app/worker .
ENV DOTNET_gcServer=0
USER $APP_UID
ENTRYPOINT ["dotnet", "FinCore.Worker.dll"]
