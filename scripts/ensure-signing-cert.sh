#!/usr/bin/env bash
# Creates a signing PFX on first run if missing (for Fly.io volume at /data/certs).
# Requires OAuthServer__SigningCertificatePassword (or SIGNING_CERT_PASSWORD).
set -euo pipefail

CERT_DIR="${SIGNING_CERT_DIR:-/data/certs}"
CERT_PATH="${OAuthServer__SigningCertificatePath:-${CERT_DIR}/signing.pfx}"
PASSWORD="${OAuthServer__SigningCertificatePassword:-${SIGNING_CERT_PASSWORD:-}}"

export OAuthServer__SigningCertificatePath="${CERT_PATH}"

if [[ -f "${CERT_PATH}" ]]; then
  echo "Signing certificate already present at ${CERT_PATH}"
  exit 0
fi

if [[ -z "${PASSWORD}" ]]; then
  echo "ERROR: ${CERT_PATH} is missing and no password is set." >&2
  echo "Set fly secret OAuthServer__SigningCertificatePassword before first deploy." >&2
  exit 1
fi

CERT_HOST="${SIGNING_CERT_HOST:-}"
if [[ -z "${CERT_HOST}" && -n "${FLY_APP_NAME:-}" ]]; then
  CERT_HOST="${FLY_APP_NAME}.fly.dev"
fi
if [[ -z "${CERT_HOST}" ]]; then
  ISSUER="${OAuthServer__Issuer:-}"
  if [[ -n "${ISSUER}" ]]; then
    CERT_HOST="$(echo "${ISSUER}" | sed -e 's#^[a-zA-Z]*://##' -e 's#/.*$##' -e 's/:.*$//')"
  fi
fi
if [[ -z "${CERT_HOST}" ]]; then
  echo "ERROR: Cannot determine certificate hostname. Set SIGNING_CERT_HOST or OAuthServer__Issuer." >&2
  exit 1
fi

mkdir -p "$(dirname "${CERT_PATH}")"
TMP_DIR="$(mktemp -d)"
trap 'rm -rf "${TMP_DIR}"' EXIT

KEY="${TMP_DIR}/signing.key"
CRT="${TMP_DIR}/signing.crt"

echo "Generating signing certificate for host ${CERT_HOST} at ${CERT_PATH} ..."
openssl genrsa -out "${KEY}" 4096 2>/dev/null
openssl req -new -x509 \
  -key "${KEY}" \
  -out "${CRT}" \
  -days 825 \
  -subj "/CN=CustomOAuthServer Signing/O=CustomOAuth" \
  -addext "subjectAltName=DNS:${CERT_HOST},DNS:localhost" 2>/dev/null

openssl pkcs12 -export \
  -out "${CERT_PATH}" \
  -inkey "${KEY}" \
  -in "${CRT}" \
  -password "pass:${PASSWORD}"

chmod 600 "${CERT_PATH}"
echo "Created signing PFX at ${CERT_PATH}"
