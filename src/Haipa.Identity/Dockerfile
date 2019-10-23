FROM microsoft/dotnet:2.2-aspnetcore-runtime AS base
WORKDIR /app
EXPOSE 80

FROM microsoft/dotnet:2.2-sdk AS build
WORKDIR /src
COPY ["src/Haipa.Identity/Haipa.Identity.csproj", "src/Haipa.Identity/"]
RUN dotnet restore "src/Haipa.Identity/Haipa.Identity.csproj"
COPY . .
WORKDIR "/src/src/Haipa.Identity"
RUN dotnet build "Haipa.Identity.csproj" -c Release -o /app

FROM build AS publish
RUN dotnet publish "Haipa.Identity.csproj" -c Release -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "Haipa.Identity.dll"]