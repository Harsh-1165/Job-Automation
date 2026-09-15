FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY JobAutomation.sln ./
COPY JobAutomation.Api/JobAutomation.Api.csproj JobAutomation.Api/
COPY JobAutomation.Application/JobAutomation.Application.csproj JobAutomation.Application/
COPY JobAutomation.Domain/JobAutomation.Domain.csproj JobAutomation.Domain/
COPY JobAutomation.Infrastructure/JobAutomation.Infrastructure.csproj JobAutomation.Infrastructure/
RUN dotnet restore JobAutomation.Api/JobAutomation.Api.csproj
COPY . .
RUN dotnet publish JobAutomation.Api/JobAutomation.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && groupadd --system appuser \
    && useradd --system --gid appuser --home /app appuser \
    && chown -R appuser:appuser /app
USER appuser
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build --chown=appuser:appuser /app/publish .
ENTRYPOINT ["dotnet", "JobAutomation.Api.dll"]
