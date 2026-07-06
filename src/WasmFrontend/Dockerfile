FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/WasmFrontend/WasmFrontend.csproj
RUN dotnet publish src/WasmFrontend/WasmFrontend.csproj -c Release -o /app/wwwroot

FROM nginx:alpine AS runtime
WORKDIR /usr/share/nginx/html
COPY --from=build /app/wwwroot/wwwroot .
COPY src/WasmFrontend/nginx.conf /etc/nginx/nginx.conf
EXPOSE 80
