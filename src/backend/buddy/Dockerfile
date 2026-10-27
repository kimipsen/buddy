# syntax=docker/dockerfile:1

# Build context is src/backend so Directory.Build.props / Directory.Packages.props
# (one level above this file) are visible to the restore/build steps.

# Pinned by tag and digest, so a rebuild of the same commit uses the same SDK and runtime. The SDK
# tag matches global.json at the repo root (which this build context can't see): bump both together.
# Dependabot keeps the digests current for security patches.
FROM mcr.microsoft.com/dotnet/sdk:11.0.100-rc.2@sha256:db7d1f28c0805ea69fe43f73e54cffd2a0689a0c637a6078328e27c5aab2314a AS build
WORKDIR /src

# .editorconfig carries the analyzer exceptions; without it the analyzers (warnings as errors) fail
# a build that passes everywhere else.
COPY Directory.Build.props Directory.Packages.props .editorconfig ./
COPY buddy/ buddy/
# The build context has no .git, so MinVer can't read the tags here: the deploy passes the
# version from deploy/version.sh instead (defaults keep a bare `docker build` working).
ARG BUDDY_VERSION=0.0.0-dev
ARG BUDDY_COMMIT=unknown
RUN dotnet publish buddy/buddy.csproj -c Release -o /app \
      -p:MinVerVersionOverride="$BUDDY_VERSION" -p:SourceRevisionId="$BUDDY_COMMIT"

FROM mcr.microsoft.com/dotnet/aspnet:11.0.0-rc.2@sha256:623c52ed3fad0fa423b8cc860910492364b0d6245870a415e997017d4a4f4513 AS runtime

# A home directory, because Data Protection (MealplansFeature) keeps its key ring under
# ~/.aspnet when nothing else is configured; without one the keys would live only in memory.
RUN if ! id -u aspnet >/dev/null 2>&1; then \
			groupadd --system aspnet && useradd --system --gid aspnet --create-home aspnet; \
		fi

WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER aspnet
ENTRYPOINT ["dotnet", "buddy.dll"]
