#!/usr/bin/env bash

set -euo pipefail

# This script prepares a local Compose `.env` file before the devcontainer
# starts. The file is gitignored and stores per-machine values that should not
# live in shared config, such as the host UID/GID and a repo-specific suffix for
# Docker volume names.
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
env_file="${script_dir}/.env"

# Fall back to 1000:1000 if `id` is unavailable for some reason, but prefer the
# actual host user's numeric IDs so Linux bind mounts line up cleanly inside the
# container.
user_uid=1000
user_gid=1000

# Use the repository directory name for the workspace path. Docker Compose
# project names must be lowercase and use a restricted character set, so keep a
# separately normalized value for Compose and named volumes.
workspace_basename="$(basename "$(dirname "${script_dir}")")"
devcontainer_id="$(printf '%s' "${workspace_basename}" \
  | tr '[:upper:]' '[:lower:]' \
  | sed -E 's/[^a-z0-9_-]+/-/g; s/^[-_]+//; s/[-_]+$//')"

if [ -z "${devcontainer_id}" ]; then
  devcontainer_id=devcontainer
fi

if command -v id >/dev/null 2>&1; then
  user_uid="$(id -u)"
  user_gid="$(id -g)"
fi

# If the local file already exists, leave explicitly configured UID/GID values
# alone. Repo-derived values are refreshed so copied or renamed projects cannot
# retain stale template names.
if [ -f "${env_file}" ]; then
  configured_uid="$(sed -n 's/^USER_UID=//p' "${env_file}" | tail -n 1)"
  configured_gid="$(sed -n 's/^USER_GID=//p' "${env_file}" | tail -n 1)"

  if [[ "${configured_uid}" =~ ^[0-9]+$ ]]; then
    user_uid="${configured_uid}"
  fi
  if [[ "${configured_gid}" =~ ^[0-9]+$ ]]; then
    user_gid="${configured_gid}"
  fi
fi

# Write the workspace name, Compose identifier, and host UID/GID before Compose
# evaluates docker-compose.yml.
cat >"${env_file}" <<EOF
DEVCONTAINER_ID=${devcontainer_id}
WORKSPACE_BASENAME=${workspace_basename}
USER_UID=${user_uid}
USER_GID=${user_gid}
EOF
