# syntax=docker/dockerfile:1

# Build context is src/backend so Directory.Build.props / Directory.Packages.props
# (one level above this file) are visible to the restore/build steps.

FROM mcr.microsoft.com/dotnet/nightly/sdk:11.0-preview AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY buddy/ buddy/
# The build context has no .git, so MinVer can't read the tags here: the deploy passes the
# version from deploy/version.sh instead (defaults keep a bare `docker build` working).
ARG BUDDY_VERSION=0.0.0-dev
ARG BUDDY_COMMIT=unknown
RUN dotnet publish buddy/buddy.csproj -c Release -o /app \
      -p:MinVerVersionOverride="$BUDDY_VERSION" -p:SourceRevisionId="$BUDDY_COMMIT"

FROM mcr.microsoft.com/dotnet/nightly/aspnet:11.0-preview AS runtime

RUN if ! id -u aspnet >/dev/null 2>&1; then \
			groupadd --system aspnet && useradd --system --gid aspnet aspnet; \
		fi

WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "buddy.dll"]
