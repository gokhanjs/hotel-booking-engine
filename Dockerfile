# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json dotnet-tools.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/BookingEngine.Domain/BookingEngine.Domain.csproj src/BookingEngine.Domain/
COPY src/BookingEngine.Application/BookingEngine.Application.csproj src/BookingEngine.Application/
COPY src/BookingEngine.Infrastructure/BookingEngine.Infrastructure.csproj src/BookingEngine.Infrastructure/
COPY src/BookingEngine.Api/BookingEngine.Api.csproj src/BookingEngine.Api/
RUN dotnet restore src/BookingEngine.Api/BookingEngine.Api.csproj && dotnet tool restore

COPY src/ src/
RUN dotnet publish src/BookingEngine.Api --no-restore -c Release -o /app -p:OpenApiGenerateDocuments=false \
 && dotnet ef migrations bundle --no-build --configuration Release \
      --project src/BookingEngine.Infrastructure --startup-project src/BookingEngine.Api \
      --output /app/efbundle

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra AS runtime
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "BookingEngine.Api.dll"]
