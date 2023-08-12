#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443
#RUN dotnet restore

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["BotTelegrammSearchPhone/BotTelegrammSearchPhone.csproj", "BotTelegrammSearchPhone/"]
RUN dotnet restore "BotTelegrammSearchPhone/BotTelegrammSearchPhone.csproj"
COPY . .
WORKDIR "/src/BotTelegrammSearchPhone"
RUN dotnet build "BotTelegrammSearchPhone.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "BotTelegrammSearchPhone.csproj" -c Release -o /app/publish 

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BotTelegrammSearchPhone.dll"]
