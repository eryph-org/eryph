#!/bin/sh
# Component entrypoint: install the deployment root CA into the OS trust store before starting, so the
# broker's TLS certificate validates. Rebus' RabbitMQ transport validates the server against the OS
# trust store (not an in-memory bundle), so the root must be installed here, not just pinned in config.
set -e

if [ -f /trust/root.crt ]; then
    cp /trust/root.crt /usr/local/share/ca-certificates/eryph-cluster-root.crt
    update-ca-certificates >/dev/null 2>&1 || true
fi

exec dotnet "/app/$APP_DLL" "$@"
