FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY *.csproj ./
RUN dotnet restore
COPY . ./
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
COPY localhost.pfx /app/localhost.pfx
EXPOSE 443
EXPOSE 5671
ENTRYPOINT ["dotnet", "SbamProxy.dll"]
