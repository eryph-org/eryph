# Local split-runtime mTLS cluster (podman/docker compose)

Brings up the cross-platform control plane (Identity, Controller, Compute API, Network) over the
mutual-TLS bus, with a one-shot provisioning container seeding the CA, broker TLS and enrollment
material. The Hyper-V agent and gene pool are Windows host processes, not part of this compose.

The component images COPY host-published output (no SDK/NuGet feeds in the image), so publish first.

## 1. Publish the components (and the provisioning binaries) for Linux

```sh
for app in Eryph.Identity Eryph.Controller Eryph.ApiEndpoint Eryph.Network; do
  dotnet publish "src/apps/src/$app/$app.csproj" -c Release -f net10.0 --no-self-contained \
    -o "docker/cluster/publish/$app"
done
dotnet publish dev/provisioning/Eryph.ClusterProvision/Eryph.ClusterProvision.csproj -c Release -f net10.0 \
  --no-self-contained -o docker/cluster/publish/Eryph.ClusterProvision
```

## 2. Bring it up

```sh
cd docker/cluster
podman compose up --build provision      # one-shot: CA, broker certs, trust anchor, enrollment files
podman compose up -d --build             # broker (TLS) → identity → controller/apiendpoint/network
```

## 3. Verify

```sh
podman exec eryph-cluster-rabbit-1 rabbitmqctl list_connections name peer_cert_subject   # mTLS peers
podman exec eryph-cluster-mariadb-1 mariadb -uroot -peryph -N \
  -e 'SELECT ComponentType, Status FROM eryph.ComponentRegistrations;'                    # Active components
```

Orchestration model in short: a one-shot `provision` step seeds the CA, broker TLS material, the root
trust anchor and the per-component enrollment files; one-shot `dbsetup` / `identity-dbsetup` steps create
the state and identity database schemas; then the broker comes up (TLS + SASL EXTERNAL), then identity
(self-issues, provisions its own broker user, hosts the CA), then the enrolling components enroll over
HTTPS and join the bus over mutual TLS.
