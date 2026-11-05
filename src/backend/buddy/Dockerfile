# syntax=docker/dockerfile:1

# Build context is src/backend so Directory.Build.props / Directory.Packages.props
# (one level above this file) are visible to the restore/build steps.

# Pinned by tag and digest, so a rebuild of the same commit uses the same SDK and runtime. The SDK
# tag matches global.json at the repo root (which this build context can't see): bump both together.
# Dependabot keeps the digests current for security patches.
FROM mcr.microsoft.com/dotnet/sdk:11.0.100-rc.3@sha256:3a9175b6c54b2f076817af1360f691c9994447b192faa48a1a1f112a516844c7 AS build
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

FROM mcr.microsoft.com/dotnet/aspnet:11.0.0-rc.3@sha256:bae0269049d5061ab7f49995cc1acebf54b75471c096c4a1f874e27661d44045 AS runtime

# A home directory, because Data Protection (MealplansFeature) keeps its key ring under
# ~/.aspnet when nothing else is configured; without one the keys would live only in memory.
# CVE-2026-109518 (apt, fixed in 3.2.1ubuntu0.1) isn't in the rc.3 image yet: drop the apt upgrade
# once Dependabot's digest bump brings an aspnet rebuild with the fixed apt.
RUN if ! id -u aspnet >/dev/null 2>&1; then \
			groupadd --system aspnet && useradd --system --gid aspnet --create-home aspnet; \
		fi \
	&& apt-get update \
	&& DEBIAN_FRONTEND=noninteractive apt-get install --only-upgrade -y --no-install-recommends apt libapt-pkg7.0 \
	&& rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER aspnet
ENTRYPOINT ["dotnet", "buddy.dll"]
