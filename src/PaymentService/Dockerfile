FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/PaymentService/PaymentService.csproj
RUN dotnet publish src/PaymentService/PaymentService.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 5004
ENV ASPNETCORE_URLS=http://+:5004
ENTRYPOINT ["dotnet", "PaymentService.dll"]
