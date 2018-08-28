FROM microsoft/dotnet:2.1-aspnetcore-runtime AS base
WORKDIR /app
EXPOSE 62189


FROM microsoft/dotnet:2.1-sdk AS build
WORKDIR /src
COPY src/HyperVPlus.ApiEndpoint/HyperVPlus.ApiEndpoint.csproj src/HyperVPlus.ApiEndpoint/
RUN dotnet restore src/HyperVPlus.ApiEndpoint/HyperVPlus.ApiEndpoint.csproj
COPY . .
WORKDIR /src/src/HyperVPlus.ApiEndpoint
RUN dotnet build HyperVPlus.ApiEndpoint.csproj -c Release -o /app

FROM build AS publish
RUN dotnet publish HyperVPlus.ApiEndpoint.csproj -c Release -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "HyperVPlus.ApiEndpoint.dll"]
