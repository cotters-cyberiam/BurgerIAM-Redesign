FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/DeliveryService/DeliveryService.csproj
RUN dotnet publish src/DeliveryService/DeliveryService.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 5096
ENV ASPNETCORE_URLS=http://+:5096
ENTRYPOINT ["dotnet", "DeliveryService.dll"]
