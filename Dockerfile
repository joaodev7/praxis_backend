FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Praxis.Domain/Praxis.Domain.csproj", "src/Praxis.Domain/"]
COPY ["src/Praxis.Application/Praxis.Application.csproj", "src/Praxis.Application/"]
COPY ["src/Praxis.Infrastructure/Praxis.Infrastructure.csproj", "src/Praxis.Infrastructure/"]
COPY ["src/Praxis.Api/Praxis.Api.csproj", "src/Praxis.Api/"]
RUN dotnet restore "src/Praxis.Api/Praxis.Api.csproj"
COPY . .
WORKDIR "/src/src/Praxis.Api"
RUN dotnet build "Praxis.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Praxis.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Praxis.Api.dll"]
