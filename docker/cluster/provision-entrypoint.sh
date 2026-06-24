#!/bin/sh
# One-shot provisioning for the split-runtime mTLS cluster (the "configuration container"). Creates the
# component CA on the shared file PKI, mints the broker's TLS material, derives the root trust anchor the
# components install, and cuts one-time enrollment files for the enrolling components. Idempotent: the CA
# is created on first run and reused afterwards.
set -e

PKI=/pki
BROKER=/broker-certs
TRUST=/trust
ENROLL=/enroll
IDENTITY_ENDPOINT="${IDENTITY_ENDPOINT:-https://identity:8080/}"
BROKER_DNS="${BROKER_DNS:-rabbit}"

export ERYPH_PKI_KEYSTORE=file
export ERYPH_PKI_DIRECTORY="$PKI"

mkdir -p "$BROKER" "$TRUST" "$ENROLL"

echo "== 1. CA + broker TLS material (CN=$BROKER_DNS) =="
dotnet /app/provision/eryph-cluster-provision.dll provision-broker "$BROKER_DNS" "$BROKER"
# rabbitmq.conf points cacertfile at ca.crt; the broker trust bundle is the full CA bundle.
cp "$BROKER/ca-bundle.pem" "$BROKER/ca.crt"

echo "== 2. Root trust anchor for the component OS trust stores =="
# The first certificate block in the bundle is the self-signed root; the broker presents its
# intermediate in the handshake, so trusting the root is enough to validate its chain.
awk '/-----BEGIN CERTIFICATE-----/{n++} n==1{print} /-----END CERTIFICATE-----/{if(n==1) exit}' \
    "$BROKER/ca-bundle.pem" > "$TRUST/root.crt"

echo "== 3. One-time enrollment files (identity self-issues, so it gets none) =="
# The token is bound to the component's self-reported FQDN, which is <hostname>.<dns-domain>. The
# components inherit the host's DNS search domain (the last 'search' token in resolv.conf — the same
# value .NET reports as the domain), so qualify each hostname with it. With no domain, the bare
# hostname is used.
DOMAIN="$(awk '/^search/{print $NF}' /etc/resolv.conf 2>/dev/null)"
fqdn() { if [ -n "$DOMAIN" ]; then echo "$1.$DOMAIN"; else echo "$1"; fi; }
echo "   using dns domain: '${DOMAIN:-<none>}'"

# type:hostname pairs — the hostname matches the component's compose 'hostname' / container name.
for pair in Controller:controller ComputeApi:apiendpoint Network:network; do
    type="${pair%%:*}"
    host="${pair##*:}"
    dotnet /app/identity/Eryph.Identity.dll new-enrollment \
        --type "$type" --fqdn "$(fqdn "$host")" --endpoint "$IDENTITY_ENDPOINT" \
        --out "$ENROLL/$host.json" --ttl-hours 24
done

echo "provisioning complete"
