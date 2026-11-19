# Migrador de base de datos: PowerShell 7 + sqlcmd (mssql-tools18) + los scripts de database/.
# Ejecuta database/deploy.ps1, el mismo script que se usa fuera de Docker. Contexto: raíz del repositorio.

FROM mcr.microsoft.com/dotnet/sdk:10.0
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl gnupg ca-certificates \
 && curl -fsSL https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor -o /usr/share/keyrings/microsoft-prod.gpg \
 && echo "deb [arch=amd64 signed-by=/usr/share/keyrings/microsoft-prod.gpg] https://packages.microsoft.com/ubuntu/24.04/prod noble main" \
      > /etc/apt/sources.list.d/mssql-release.list \
 && apt-get update \
 && ACCEPT_EULA=Y apt-get install -y --no-install-recommends mssql-tools18 unixodbc \
 && rm -rf /var/lib/apt/lists/*
ENV PATH="/opt/mssql-tools18/bin:${PATH}"
WORKDIR /database
COPY database/ .
ENTRYPOINT ["pwsh", "-NoLogo", "-NoProfile", "-File"]
CMD ["/database/deploy.ps1"]
