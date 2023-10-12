#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["FastTunnel.Client/FastTunnel.Client.csproj", "FastTunnel.Client/"]
COPY ["FastTunnel.Core/FastTunnel.Core.csproj", "FastTunnel.Core/"]
RUN dotnet restore "FastTunnel.Client/FastTunnel.Client.csproj"
COPY . .
WORKDIR "/src/FastTunnel.Client"
RUN dotnet build "FastTunnel.Client.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "FastTunnel.Client.csproj" -c Release -o /app/publish

FROM base AS final
#RUN mkdir -p /vols
WORKDIR /app

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "FastTunnel.Client.dll"]
