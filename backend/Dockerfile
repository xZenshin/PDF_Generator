# The API as one stateless container: nothing to mount, nothing to back up.
# Built from the backend/ directory:  docker build -t cv-builder-api backend

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore on its own layer so a code-only change doesn't re-download packages.
COPY CvBuilder.Api/CvBuilder.Api.csproj CvBuilder.Api/
RUN dotnet restore CvBuilder.Api/CvBuilder.Api.csproj

COPY . .
RUN dotnet publish CvBuilder.Api/CvBuilder.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# QuestPDF renders through SkiaSharp, which wants fontconfig present on Linux. QuestPDF
# carries its own Lato, so the PDF matches the Windows build without installing fonts.
RUN apt-get update \
 && apt-get install -y --no-install-recommends libfontconfig1 \
 && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app .

# Container Apps sends traffic to one port and expects the app to bind every interface.
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

# Provided by the .NET base images: a non-root account to drop to.
USER $APP_UID
ENTRYPOINT ["dotnet", "CvBuilder.Api.dll"]
