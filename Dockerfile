FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY CustomOAuthServer.sln ./
COPY src/CustomOAuthServer.Application/ src/CustomOAuthServer.Application/
COPY src/CustomOAuthServer.Infrastructure/ src/CustomOAuthServer.Infrastructure/
COPY src/CustomOAuthServer.Api/ src/CustomOAuthServer.Api/
COPY Migrations/ Migrations/
RUN dotnet restore src/CustomOAuthServer.Api/CustomOAuthServer.Api.csproj
RUN dotnet publish src/CustomOAuthServer.Api/CustomOAuthServer.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends openssl \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://+:8080
ENV OAUTH_AUTO_GENERATE_SIGNING_CERT=true
ENV OAuthServer__SigningCertificatePath=/data/certs/signing.pfx

EXPOSE 8080
COPY --from=build /app/publish .
COPY scripts/ensure-signing-cert.sh scripts/docker-entrypoint.sh /app/scripts/
RUN chmod +x /app/scripts/ensure-signing-cert.sh /app/scripts/docker-entrypoint.sh

ENTRYPOINT ["/app/scripts/docker-entrypoint.sh"]
