# syntax=docker/dockerfile:1

# Build context is src/backend so Directory.Build.props / Directory.Packages.props
# (one level above this file) are visible to the restore/build steps.

# Pinned by tag and digest, so a rebuild of the same commit uses the same SDK and runtime. The SDK
# tag matches global.json at the repo root (which this build context can't see): bump both together.
# Dependabot keeps the digests current for security patches.
FROM mcr.microsoft.com/dotnet/sdk:11.0.100-preview.7@sha256:b7e3c0268942d9e80656c5ff9d0514c96741e940cf3acae9f9a8cc1fcc6991be AS build
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

FROM mcr.microsoft.com/dotnet/aspnet:11.0.0-preview.7@sha256:3e4617ed8d8025e466209d5193f41faa3ebd237d8c85e725220e297422048597 AS runtime

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
