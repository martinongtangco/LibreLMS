FROM mcr.microsoft.com/devcontainers/dotnet:1-10.0

# The base image bundles whatever SDK build was current when the image tag was published
# (an RC build, as of writing) — not necessarily the exact GA SDK this repo pins in
# global.json. Install that exact version explicitly so the container always matches.
USER root
RUN curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh \
    && bash /tmp/dotnet-install.sh --version 10.0.103 --install-dir /usr/share/dotnet \
    && rm /tmp/dotnet-install.sh

# Pi Agent CLI (the coding agent that will actually write code here) is an npm package
# that needs Node 20+ (it uses the ES2024 `v` regex flag) — the base image has neither
# Node nor npm, and its stock Ubuntu nodejs package is only 18.x. Installed here, not on
# the host, because the whole point of the sandbox (constitution Principle V) is that the
# agent's own process runs inside this container, not on the host reaching in through the
# bind mount.
#
# The base image also ships a broken /etc/apt/sources.list.d/yarn.list (unsigned repo,
# unrelated to us) that aborts `apt-get update` before NodeSource's own setup script can
# register its repo — remove it first or Node silently resolves to Ubuntu's stale 18.x.
RUN rm -f /etc/apt/sources.list.d/yarn.list \
    && curl -fsSL https://deb.nodesource.com/setup_22.x | bash - \
    && apt-get install -y nodejs \
    && rm -rf /var/lib/apt/lists/*
RUN npm install -g @earendil-works/pi-coding-agent

# The base image already configures a non-root 'vscode' user — the agent's process
# runs as this user, never root, inside the container.
USER vscode

RUN dotnet tool install --global dotnet-ef
ENV PATH="${PATH}:/home/vscode/.dotnet/tools"
