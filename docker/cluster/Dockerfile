# syntax=docker/dockerfile:1
#
# Thin runtime image for a cross-platform eryph split-runtime component.
# We publish on the host (the repo already builds for net10.0) and only COPY the
# framework-dependent output in here — so the image needs no SDK, no NuGet feeds
# and no repo build context. Pick the component with the APP build-arg
# (Eryph.Identity | Eryph.Controller | Eryph.ApiEndpoint).
FROM mcr.microsoft.com/dotnet/aspnet:10.0

ARG APP
ARG INSTALL_OVN_CLIENT=false
ARG INSTALL_OVN_SERVER=false

# OVN packaging is role-specific:
#   - Controller is an OVN *client* — it shells out to ovn-nbctl to write the northbound DB
#     that Eryph.Network hosts. It needs only ovn-common. (INSTALL_OVN_CLIENT=true)
#   - Eryph.Network is the OVN *host* — it runs the NB/SB databases (ovsdb-server) and
#     ovn-northd. These are userspace daemons (no kernel datapath on the NB/SB side, so they
#     run in a plain container); the chassis/datapath lives on the Hyper-V host agent.
#     (INSTALL_OVN_SERVER=true -> ovn-central pulls in ovn-common + openvswitch ovsdb-server)
#   - Identity/ApiEndpoint stay thin (both flags default false).
# ca-certificates provides update-ca-certificates, which the entrypoint uses to trust the deployment
# root CA so the broker's mutual-TLS certificate validates against the OS trust store.
RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates \
    && if [ "$INSTALL_OVN_SERVER" = "true" ]; then \
           apt-get install -y --no-install-recommends ovn-central ovn-common openvswitch-common; \
       elif [ "$INSTALL_OVN_CLIENT" = "true" ]; then \
           apt-get install -y --no-install-recommends ovn-common; \
       fi \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY publish/${APP}/ ./
COPY entrypoint.sh /usr/local/bin/entrypoint.sh
RUN mkdir -p /var/lib/eryph/config && chmod +x /usr/local/bin/entrypoint.sh
ENV APP_DLL=${APP}.dll

# The entrypoint installs the deployment root CA, then exec's the component.
ENTRYPOINT ["/usr/local/bin/entrypoint.sh"]
