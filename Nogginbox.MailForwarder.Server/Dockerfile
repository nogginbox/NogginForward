#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:7.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["Nogginbox.MailForwarder.Server/Nogginbox.MailForwarder.Server.csproj", "Nogginbox.MailForwarder.Server/"]
RUN dotnet restore "Nogginbox.MailForwarder.Server/Nogginbox.MailForwarder.Server.csproj"
COPY . .
WORKDIR "/src/Nogginbox.MailForwarder.Server"
RUN dotnet build "Nogginbox.MailForwarder.Server.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Nogginbox.MailForwarder.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

EXPOSE 25 587 465
ENTRYPOINT ["dotnet", "Nogginbox.MailForwarder.Server.dll"]