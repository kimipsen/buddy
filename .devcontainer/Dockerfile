# The .NET SDK pinned in global.json at the repo root: bump both together (and the tags in
# src/backend/buddy/Dockerfile with them).
FROM mcr.microsoft.com/dotnet/sdk:11.0.100 AS dotnet-sdk

FROM mcr.microsoft.com/devcontainers/dotnet:11.0

# The devcontainers/dotnet:11.0 image still ships the 11.0.100-rc.3 SDK, which global.json no
# longer accepts. Swap in the official SDK image's install (same Ubuntu release, same
# /usr/share/dotnet layout). Drop this once the base image ships 11.0.100 or later.
RUN rm -rf /usr/share/dotnet
COPY --from=dotnet-sdk /usr/share/dotnet /usr/share/dotnet

# [Optional] Uncomment this section to install additional OS packages.
# RUN apt-get update && export DEBIAN_FRONTEND=noninteractive \
#     && apt-get -y install --no-install-recommends <your-package-list-here>

RUN if ! id -u vscode >/dev/null 2>&1; then \
			addgroup -S vscode && adduser -S vscode -G vscode; \
		fi

# [Optional] Uncomment this line to install global node packages.
# RUN su vscode -c "source /usr/local/share/nvm/nvm.sh && npm install -g <your-package-here>" 2>&1