FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/KitchenService/KitchenService.csproj
RUN dotnet publish src/KitchenService/KitchenService.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 5085
ENV ASPNETCORE_URLS=http://+:5085
ENTRYPOINT ["dotnet", "KitchenService.dll"]
