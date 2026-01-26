#!/usr/bin/env bash
set -euo pipefail

# Smoke test for POC S3 bucket:
# - Assumes the presigner role
# - Presigns safe object and verifies download
# - Presigns poisoned object and verifies access is denied

usage() {
  cat <<'EOF'
Usage: s3_smoke.sh [-b bucket] [-r role_arn] [-R region]
  -b bucket      S3 bucket name (defaults to terraform output poc_bucket_name)
  -r role_arn    Presigner role ARN (defaults to terraform output poc_presigner_role_arn)
  -R region      AWS region (defaults to AWS_REGION/AWS_DEFAULT_REGION or terraform output aws_region)
Environment overrides:
  SAFE_KEY (default: samples/safe.txt)
  POISON_KEY (default: samples/poisoned.txt)
EOF
}

bucket=""
role_arn=""
region="${AWS_REGION:-${AWS_DEFAULT_REGION:-}}"
while getopts ":b:r:R:h" opt; do
  case "$opt" in
    b) bucket="$OPTARG" ;;
    r) role_arn="$OPTARG" ;;
    R) region="$OPTARG" ;;
    h) usage; exit 0 ;;
    *) usage; exit 1 ;;
  esac
done

terraform_dir="$(cd "$(dirname "$0")/../terraform" && pwd)"
safe_key="${SAFE_KEY:-samples/safe.txt}"
poison_key="${POISON_KEY:-samples/poisoned.txt}"

require_cmd() { command -v "$1" >/dev/null 2>&1 || { echo "Missing command: $1" >&2; exit 1; }; }
require_cmd aws
require_cmd curl

# Helper to read a terraform output
read_tf_output() {
  local name="$1"
  TOFU_CMD="tofu"
  if ! command -v tofu >/dev/null 2>&1; then
    TOFU_CMD="terraform"
  fi
  (cd "$terraform_dir" && $TOFU_CMD output -raw "$name")
}

if [[ -z "$bucket" ]]; then
  bucket=$(read_tf_output poc_bucket_name)
fi

if [[ -z "$role_arn" ]]; then
  role_arn=$(read_tf_output poc_presigner_role_arn)
fi

if [[ -z "$region" ]]; then
  region=$(read_tf_output aws_region 2>/dev/null || true)
fi

if [[ -z "$bucket" || -z "$role_arn" ]]; then
  echo "Bucket or role ARN not provided and not found via terraform outputs." >&2
  exit 1
fi

# Assume role and capture temp creds
read AK SK STS <<<"$(aws sts assume-role \
  --role-arn "$role_arn" \
  --role-session-name s3-presign-smoke \
  --duration-seconds 900 \
  ${region:+--region "$region"} \
  --query 'Credentials.[AccessKeyId,SecretAccessKey,SessionToken]' \
  --output text)"

export AWS_ACCESS_KEY_ID="$AK"
export AWS_SECRET_ACCESS_KEY="$SK"
export AWS_SESSION_TOKEN="$STS"
if [[ -n "$region" ]]; then
  export AWS_REGION="$region"
  export AWS_DEFAULT_REGION="$region"
fi

echo "Using bucket: $bucket"
echo "Presigner role: $role_arn"
echo "Region: ${region:-default}"

tmpdir=$(mktemp -d)
cleanup() { rm -rf "$tmpdir"; }
trap cleanup EXIT

presign() {
  local key="$1"
  aws s3 presign "s3://$bucket/$key" --expires-in 60
}

test_download() {
  local key="$1" expected_ok="$2" label="$3"
  local url status
  url=$(presign "$key")
  status=$(curl -s -o "$tmpdir/$label.out" -w "%{http_code}" "$url")

  if [[ "$expected_ok" == "yes" ]]; then
    if [[ "$status" != "200" ]]; then
      echo "FAIL: $label expected 200, got $status" >&2
      return 1
    fi
    echo "PASS: $label (status $status)"
    head -n 1 "$tmpdir/$label.out" | sed 's/^/  body: /'
  else
    if [[ "$status" == "200" ]]; then
      echo "FAIL: $label expected denial, got 200" >&2
      return 1
    fi
    echo "PASS: $label denied as expected (status $status)"
  fi
}

# Run tests
ok=0
if ! test_download "$safe_key" "yes" "safe"; then ok=1; fi
if ! test_download "$poison_key" "no" "poisoned"; then ok=1; fi
exit $ok
