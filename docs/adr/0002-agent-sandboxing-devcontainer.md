# 0002: AI Coding Agent Sandboxing via Dev Container

## Status
Accepted — 2026-07-28

## Context
The user's actual coding agent for this project (Pi Agent CLI, driving a local Qwen model) will
run with read/bash/edit/write tools against this repo. Before letting an autonomous agent run
shell commands and edit files unsupervised, we want a concrete answer to "what's the worst it
could do to this machine" — and to make that answer small on purpose, not just by convention.

Options considered:
1. **No isolation** — agent runs directly on the host with the same permissions as the person
   running it. Zero setup cost, but a bug or a bad instruction (or a prompt-injected one, if the
   agent ever reads untrusted content) can touch anything the host user account can touch:
   other projects, SSH keys, browser profiles, the works.
2. **Windows Sandbox** — a disposable, fully isolated Windows VM built into Windows 11 Pro. Very
   strong isolation (separate kernel), but disposable-by-design: nothing persists between
   sessions unless explicitly configured, which fights against an iterative coding workflow where
   you want the repo state to survive between agent runs. Also Windows-only, so this setup
   wouldn't transfer if development ever moves to Linux/macOS.
3. **WSL2 distro** — isolates from the Windows host filesystem, but the agent would still have
   full access to that distro's entire filesystem and network, which is a much bigger surface
   than "this one repo."
4. **Dev Container** (Docker container defined by `.devcontainer/`) — the agent's process runs
   *inside* a container that bind-mounts only this repo. No other host path is visible from
   inside. Persists naturally (the container/volumes stick around between sessions unless you
   remove them). Cross-platform — the same `.devcontainer/` works on Linux/macOS too.

## Decision
Use a Dev Container as the agent's sandbox. Concretely:
- `docker-compose.yml`'s `devcontainer` service bind-mounts **only** `.:/workspace` — no other
  host directory is mounted in.
- The container runs as the non-root `vscode` user (see `.devcontainer/Dockerfile`), not root —
  even a container escape would first need a privilege escalation, not just an escape.
- Sibling services (`mssql`, `valkey`) are reachable by service name on the compose network; the
  host's other services are not, because the agent's process never runs on the host network at all.

## Consequences — what this actually protects against, and what it doesn't
**Protects against:** the agent (or a bug, or a bad instruction) reading, modifying, or deleting
anything outside this repo on the host filesystem. That's the main risk of an autonomous
edit/write-capable agent, and it's fully closed off — there is no path from inside the container
to, say, `C:\Users\<you>\Documents` or another project's `.git` credentials.

**Does not (yet) protect against:** unrestricted outbound network access. The container can still
reach the internet (needed for `dotnet restore`/NuGet), so this setup does not stop the agent from
exfiltrating data over the network or downloading and running arbitrary code from the internet if
instructed to. A tighter version of this sandbox would add an egress allowlist (e.g. only
`api.nuget.org` and the sibling containers) via a custom Docker network or a local NuGet proxy —
deliberately deferred as a later, optional hardening step rather than blocking the basic dev loop
on it now. If this project continues past the learning-exercise stage, that's the next boundary to
close.
