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
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CustomOAuthServer.Api.dll"]
