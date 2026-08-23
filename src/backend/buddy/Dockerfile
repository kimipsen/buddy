# syntax=docker/dockerfile:1

# Build context is src/backend so Directory.Build.props / Directory.Packages.props
# (one level above this file) are visible to the restore/build steps.

FROM mcr.microsoft.com/dotnet/nightly/sdk:11.0-preview AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY buddy/buddy.csproj buddy/
RUN dotnet restore buddy/buddy.csproj

COPY buddy/ buddy/
RUN dotnet publish buddy/buddy.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/nightly/aspnet:11.0-preview AS runtime
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "buddy.dll"]
