FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/ProgressPlayReporting.Api/ProgressPlayReporting.Api.csproj", "src/ProgressPlayReporting.Api/"]
COPY ["src/ProgressPlayReporting.Core/ProgressPlayReporting.Core.csproj", "src/ProgressPlayReporting.Core/"]
COPY ["src/ProgressPlayReporting.SchemaExtractor/ProgressPlayReporting.SchemaExtractor.csproj", "src/ProgressPlayReporting.SchemaExtractor/"]
COPY ["src/ProgressPlayReporting.LlmIntegration/ProgressPlayReporting.LlmIntegration.csproj", "src/ProgressPlayReporting.LlmIntegration/"]
COPY ["src/ProgressPlayReporting.Validators/ProgressPlayReporting.Validators.csproj", "src/ProgressPlayReporting.Validators/"]
RUN dotnet restore "src/ProgressPlayReporting.Api/ProgressPlayReporting.Api.csproj"
COPY . .
WORKDIR "/src/src/ProgressPlayReporting.Api"
RUN dotnet build "ProgressPlayReporting.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ProgressPlayReporting.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ProgressPlayReporting.Api.dll"]
