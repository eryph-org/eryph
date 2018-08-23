FROM microsoft/dotnet:2.1-sdk-alpine AS build
WORKDIR /src

RUN apk add --no-cache bash
COPY src/HyperVPlus.Controller/HyperVPlus.Controller.csproj src/HyperVPlus.Controller/
RUN dotnet restore src/HyperVPlus.Controller/HyperVPlus.Controller.csproj
COPY . .
WORKDIR /src/src/HyperVPlus.Controller
RUN dotnet build HyperVPlus.Controller.csproj -c Release -o /app

FROM build AS publish
RUN dotnet publish HyperVPlus.Controller.csproj -c Release -o /app

FROM microsoft/dotnet:2.0-runtime-alpine AS runtime
WORKDIR /app
COPY --from=build /app/dotnetapp/out ./
ENTRYPOINT ["dotnet", "HyperVPlus.Controller.dll"]