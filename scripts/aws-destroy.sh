#!/usr/bin/env bash
set -euo pipefail

AUTO_APPROVE=false
if [[ "${1:-}" == "--auto-approve" ]]; then
  AUTO_APPROVE=true
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
INFRA_DIR="${REPO_ROOT}/infrastructure"

cd "${INFRA_DIR}"

echo "==> OpenTofu init"
tofu init -reconfigure -backend-config backend.hcl

echo "==> OpenTofu destroy"
if [[ "${AUTO_APPROVE}" == "true" ]]; then
  tofu destroy -auto-approve
else
  tofu destroy
fi

