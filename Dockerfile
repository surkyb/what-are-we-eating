# Imagen de WhatAreWeEating. Dos destinos: "api" (por defecto) y "worker" (MailWorker).
# No contiene claves ni contraseñas: todo llega por variables de entorno en tiempo de ejecución.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Primero solo los .csproj: la restauración queda en caché mientras no cambien las dependencias
COPY src/WhatAreWeEating.Core/*.csproj src/WhatAreWeEating.Core/
COPY src/WhatAreWeEating.Recetas/*.csproj src/WhatAreWeEating.Recetas/
COPY src/WhatAreWeEating.Infrastructure/*.csproj src/WhatAreWeEating.Infrastructure/
COPY src/WhatAreWeEating.Api/*.csproj src/WhatAreWeEating.Api/
COPY src/WhatAreWeEating.MailWorker/*.csproj src/WhatAreWeEating.MailWorker/
RUN dotnet restore src/WhatAreWeEating.Api && dotnet restore src/WhatAreWeEating.MailWorker

COPY src/ src/
RUN dotnet publish src/WhatAreWeEating.Api -c Release --no-restore -o /out/api \
 && dotnet publish src/WhatAreWeEating.MailWorker -c Release --no-restore -o /out/worker

# --- MailWorker ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS worker
WORKDIR /app
COPY --from=build /out/worker .
USER $APP_UID
ENTRYPOINT ["dotnet", "WhatAreWeEating.MailWorker.dll"]

# --- API (destino por defecto: es el último) ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api
WORKDIR /app
COPY --from=build /out/api .

# Carpeta de los archivos de documentos: el compose monta aquí un volumen con nombre.
# Se crea con el dueño del usuario sin privilegios para que el volumen nuevo herede el permiso de escritura.
RUN mkdir -p /data/documentos && chown -R $APP_UID /data

ENV ASPNETCORE_HTTP_PORTS=8080 \
    Documentos__Ruta=/data/documentos
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "WhatAreWeEating.Api.dll"]
