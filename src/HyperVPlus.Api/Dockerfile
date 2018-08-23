FROM microsoft/dotnet:2.1-aspnetcore-runtime AS base
WORKDIR /app
EXPOSE 62189


FROM microsoft/dotnet:2.1-sdk AS build
WORKDIR /src
COPY src/HyperVPlus.Api/HyperVPlus.Api.csproj src/HyperVPlus.Api/
RUN dotnet restore src/HyperVPlus.Api/HyperVPlus.Api.csproj
COPY . .
WORKDIR /src/src/HyperVPlus.Api
RUN dotnet build HyperVPlus.Api.csproj -c Release -o /app

FROM build AS publish
RUN dotnet publish HyperVPlus.Api.csproj -c Release -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "HyperVPlus.Api.dll"]
