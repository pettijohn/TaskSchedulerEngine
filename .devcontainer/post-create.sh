#!/usr/bin/env bash

set -euo pipefail

# Mark the current workspace as safe for Git in case the bind-mounted repo is
# owned by a different numeric UID on the host.
git config --global --add safe.directory "$(pwd)"

# Warn about common VS Code state ownership problems without mutating anything.
# If these paths are root-owned, extension installs and remote server startup
# can fail even though the container itself otherwise looks healthy.
check_owner_path() {
  local path="$1"
  local expected_uid
  local expected_gid
  local actual_uid
  local actual_gid

  if [ ! -e "${path}" ]; then
    return
  fi

  expected_uid="$(id -u)"
  expected_gid="$(id -g)"
  actual_uid="$(stat -c '%u' "${path}")"
  actual_gid="$(stat -c '%g' "${path}")"

  if [ "${actual_uid}" != "${expected_uid}" ] || [ "${actual_gid}" != "${expected_gid}" ]; then
    echo "WARNING: ${path} is owned by ${actual_uid}:${actual_gid}, expected ${expected_uid}:${expected_gid}."
    echo "This can break VS Code server startup, extension installs, or shared editor state."
  fi
}

# Print the effective runtime identity so it is easy to verify that the
# container user matches the expected host UID/GID mapping.
echo "Devcontainer setup complete."
echo "User: $(whoami)"
echo "UID: $(id -u)"
echo "GID: $(id -g)"
echo "Home: $HOME"

# Check the main VS Code server paths that are commonly backed by named volumes
# or bind mounts and therefore most prone to stale ownership problems.
check_owner_path "$HOME/.vscode-server"
check_owner_path "$HOME/.vscode-server/extensions"
check_owner_path "$HOME/.vscode-server/extensionsCache"
check_owner_path "$HOME/.vscode-server/data/Machine"
