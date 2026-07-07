FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/OrderService/OrderService.csproj
RUN dotnet publish src/OrderService/OrderService.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 5063
ENV ASPNETCORE_URLS=http://+:5063
ENTRYPOINT ["dotnet", "OrderService.dll"]
