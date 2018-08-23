FROM microsoft/aspnetcore:2.0 AS base
WORKDIR /app
EXPOSE 80

FROM microsoft/aspnetcore-build:2.0 AS build
WORKDIR /src
COPY src/HyperVPlus.Controller/HyperVPlus.Controller.csproj src/HyperVPlus.Controller/
RUN dotnet restore src/HyperVPlus.Controller/HyperVPlus.Controller.csproj
COPY . .
WORKDIR /src/src/HyperVPlus.Controller
RUN dotnet build HyperVPlus.Controller.csproj -c Release -o /app

FROM build AS publish
RUN dotnet publish HyperVPlus.Controller.csproj -c Release -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "HyperVPlus.Controller.dll"]
