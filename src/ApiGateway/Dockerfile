FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .

# Build Blazor WASM frontend
RUN dotnet restore src/WasmFrontend/WasmFrontend.csproj
RUN dotnet publish src/WasmFrontend/WasmFrontend.csproj -c Release -o /wasm

# Build API Gateway
RUN dotnet restore src/ApiGateway/ApiGateway.csproj
RUN dotnet publish src/ApiGateway/ApiGateway.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app .
COPY --from=build /wasm/wwwroot ./wwwroot
EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000
ENTRYPOINT ["dotnet", "ApiGateway.dll"]
