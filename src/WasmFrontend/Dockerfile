FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/WasmFrontend/WasmFrontend.csproj
RUN dotnet publish src/WasmFrontend/WasmFrontend.csproj -c Release -o /app/wwwroot

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/wwwroot ./wwwroot
EXPOSE 80
# Use dotnet serve or nginx to serve static files
# For nginx, swap this base image
ENTRYPOINT ["dotnet", "WasmFrontend.dll"]
