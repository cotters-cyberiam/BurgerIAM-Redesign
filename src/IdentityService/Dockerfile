FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/IdentityService/IdentityService.csproj
RUN dotnet publish src/IdentityService/IdentityService.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 5041
ENV ASPNETCORE_URLS=http://+:5041
ENTRYPOINT ["dotnet", "IdentityService.dll"]
