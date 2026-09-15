FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY JobAutomation.sln ./
COPY JobAutomation.Worker/JobAutomation.Worker.csproj JobAutomation.Worker/
COPY JobAutomation.Application/JobAutomation.Application.csproj JobAutomation.Application/
COPY JobAutomation.Domain/JobAutomation.Domain.csproj JobAutomation.Domain/
COPY JobAutomation.Infrastructure/JobAutomation.Infrastructure.csproj JobAutomation.Infrastructure/
RUN dotnet restore JobAutomation.Worker/JobAutomation.Worker.csproj
COPY . .
RUN dotnet publish JobAutomation.Worker/JobAutomation.Worker.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
RUN groupadd --system appuser \
    && useradd --system --gid appuser --home /app appuser \
    && chown -R appuser:appuser /app
USER appuser
COPY --from=build --chown=appuser:appuser /app/publish .
ENTRYPOINT ["dotnet", "JobAutomation.Worker.dll"]
