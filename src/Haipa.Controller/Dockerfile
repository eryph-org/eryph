FROM microsoft/dotnet:2.1-sdk-alpine AS build
WORKDIR /src

RUN apk add --no-cache bash
COPY src/Haipa.Controller/Haipa.Controller.csproj src/Haipa.Controller/
RUN dotnet restore src/Haipa.Controller/Haipa.Controller.csproj
COPY . .
WORKDIR /src/src/Haipa.Controller
RUN dotnet build Haipa.Controller.csproj -c Release -o /app

FROM build AS publish
RUN dotnet publish Haipa.Controller.csproj -c Release -o /app

FROM microsoft/dotnet:2.0-runtime-alpine AS runtime
WORKDIR /app
COPY --from=build /app/dotnetapp/out ./
ENTRYPOINT ["dotnet", "Haipa.Controller.dll"]