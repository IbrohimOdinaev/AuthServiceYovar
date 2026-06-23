#!/usr/bin/env sh
set -eu

CERT_DIR="$(dirname "$0")/certs"
PASSWORD="local-compose-password"

mkdir -p "$CERT_DIR"

generate_certificate() {
  name="$1"
  subject="$2"

  if [ -f "$CERT_DIR/$name.pfx" ]; then
    echo "$CERT_DIR/$name.pfx already exists"
    if [ "$name" = "authservice-signing" ] && [ ! -f "$CERT_DIR/$name.cer" ]; then
      openssl pkcs12 \
        -in "$CERT_DIR/$name.pfx" \
        -clcerts \
        -nokeys \
        -out "$CERT_DIR/$name.cer" \
        -passin "pass:$PASSWORD"
      chmod 0644 "$CERT_DIR/$name.cer"
      echo "generated $CERT_DIR/$name.cer"
    fi
    return
  fi

  openssl req \
    -x509 \
    -newkey rsa:3072 \
    -keyout "$CERT_DIR/$name.key" \
    -out "$CERT_DIR/$name.crt" \
    -days 3650 \
    -nodes \
    -subj "$subject"

  openssl pkcs12 \
    -export \
    -out "$CERT_DIR/$name.pfx" \
    -inkey "$CERT_DIR/$name.key" \
    -in "$CERT_DIR/$name.crt" \
    -passout "pass:$PASSWORD"

  chmod 0644 "$CERT_DIR/$name.pfx"
  if [ "$name" = "authservice-signing" ]; then
    openssl pkcs12 \
      -in "$CERT_DIR/$name.pfx" \
      -clcerts \
      -nokeys \
      -out "$CERT_DIR/$name.cer" \
      -passin "pass:$PASSWORD"
    chmod 0644 "$CERT_DIR/$name.cer"
    echo "generated $CERT_DIR/$name.cer"
  fi
  rm "$CERT_DIR/$name.key" "$CERT_DIR/$name.crt"
  echo "generated $CERT_DIR/$name.pfx"
}

generate_certificate "authservice-signing" "/CN=AuthService Staging Signing"
generate_certificate "authservice-encryption" "/CN=AuthService Staging Encryption"

if [ ! -f "$CERT_DIR/demo-proxy.crt" ] || [ ! -f "$CERT_DIR/demo-proxy.key" ]; then
  openssl req \
    -x509 \
    -newkey rsa:2048 \
    -keyout "$CERT_DIR/demo-proxy.key" \
    -out "$CERT_DIR/demo-proxy.crt" \
    -days 3650 \
    -nodes \
    -subj "/CN=localhost" \
    -addext "subjectAltName=DNS:localhost,IP:127.0.0.1"

  chmod 0644 "$CERT_DIR/demo-proxy.crt" "$CERT_DIR/demo-proxy.key"
  echo "generated $CERT_DIR/demo-proxy.crt"
  echo "generated $CERT_DIR/demo-proxy.key"
fi

echo "done"
