# Stage 1: base
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 443

# Install Kerberos GSSAPI library for Npgsql on Linux platforms
RUN if [ "$TARGETPLATFORM" = "linux/amd64" ] || [ "$TARGETPLATFORM" = "linux/arm64" ]; then \
    apt-get update && apt-get install -y libgssapi-krb5-3 && rm -rf /var/lib/apt/lists/*; \
    fi

# Stage 2: build
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Cadmus.Bricks.Api/Cadmus.Bricks.Api.csproj", "Cadmus.Bricks.Api/"]
RUN dotnet restore "Cadmus.Bricks.Api/Cadmus.Bricks.Api.csproj" -s https://api.nuget.org/v3/index.json --verbosity n
# copy the content of the API project
COPY . .
# build it
RUN dotnet build "Cadmus.Bricks.Api/Cadmus.Bricks.Api.csproj" -c Release -o /app/build

# Stage 3: publish
FROM build AS publish
RUN dotnet publish "Cadmus.Bricks.Api/Cadmus.Bricks.Api.csproj" -c Release -o /app/publish

# Stage 4: final
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Cadmus.Bricks.Api.dll"]
