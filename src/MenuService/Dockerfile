FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/MenuService/MenuService.csproj
RUN dotnet publish src/MenuService/MenuService.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 5052
ENV ASPNETCORE_URLS=http://+:5052
ENTRYPOINT ["dotnet", "MenuService.dll"]
