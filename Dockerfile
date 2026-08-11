# Blue4Learn — ASP.NET Core 8
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Blue4Learn.sln ./
COPY src/Blue4Learn.Web/Blue4Learn.Web.csproj src/Blue4Learn.Web/
RUN dotnet restore src/Blue4Learn.Web/Blue4Learn.Web.csproj

COPY src/Blue4Learn.Web/ src/Blue4Learn.Web/
RUN dotnet publish src/Blue4Learn.Web/Blue4Learn.Web.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

RUN mkdir -p /data /app/App_Data \
    && chown -R app:app /data /app

ENV ASPNETCORE_URLS=http://+:8080 \
    ConnectionStrings__DefaultConnection="Data Source=/app/App_Data/app.db;Cache=Shared"

EXPOSE 8080

COPY --from=build /app/publish .
USER app

ENTRYPOINT ["dotnet", "Blue4Learn.Web.dll"]
