FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/ReceiptService/ReceiptService.csproj
RUN dotnet publish src/ReceiptService/ReceiptService.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 5029
ENV ASPNETCORE_URLS=http://+:5029
ENTRYPOINT ["dotnet", "ReceiptService.dll"]
