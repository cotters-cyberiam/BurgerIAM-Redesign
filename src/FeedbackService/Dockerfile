FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/FeedbackService/FeedbackService.csproj
RUN dotnet publish src/FeedbackService/FeedbackService.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 5007
ENV ASPNETCORE_URLS=http://+:5007
ENTRYPOINT ["dotnet", "FeedbackService.dll"]
