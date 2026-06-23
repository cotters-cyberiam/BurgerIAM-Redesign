FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/NotificationService/NotificationService.csproj
RUN dotnet publish src/NotificationService/NotificationService.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 5018
ENV ASPNETCORE_URLS=http://+:5018
ENTRYPOINT ["dotnet", "NotificationService.dll"]
