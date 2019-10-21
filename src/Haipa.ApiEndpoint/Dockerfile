FROM microsoft/dotnet:2.2-aspnetcore-runtime AS base
WORKDIR /app
EXPOSE 80

FROM microsoft/dotnet:2.2-sdk AS build
WORKDIR /src
COPY ["src/Haipa.ApiEndpoint/Haipa.ApiEndpoint.csproj", "src/Haipa.ApiEndpoint/"]
RUN dotnet restore "src/Haipa.ApiEndpoint/Haipa.ApiEndpoint.csproj"
COPY . .
WORKDIR "/src/src/Haipa.ApiEndpoint"
RUN dotnet build "Haipa.ApiEndpoint.csproj" -c Release -o /app

FROM build AS publish
RUN dotnet publish "Haipa.ApiEndpoint.csproj" -c Release -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "Haipa.ApiEndpoint.dll"]