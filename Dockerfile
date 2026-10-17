# THE LAST SIGNAL - Dedicated Server container image (TLS-E03-S04)
#
# Expects the Linux Dedicated Server build to already exist at Builds/DedicatedServer/Linux/
# (Unity Editor menu: TheLastSignal/Build/Dedicated Server (Linux)). This Dockerfile only packages
# that output — it does not invoke Unity itself.
#
# Base: Debian slim. Unity's Linux Standalone/Server builds are glibc binaries, not musl, so an
# Alpine base will NOT run them without extra work — Debian/Ubuntu-family bases are the safe default.
FROM debian:bookworm-slim

# Common runtime shared libraries Unity Linux players typically need. If the container fails to start
# with a "shared library not found" style error, run `ldd TheLastSignalServer` inside the container
# (see docs/server/RUNBOOK_SERVER.md) and add whatever's missing here — this list is a sensible
# starting point, not verified against an actual run on this machine (Docker Desktop's engine was not
# running in this session — see the runbook's manual prerequisites).
RUN apt-get update && apt-get install -y --no-install-recommends \
        ca-certificates \
        libatomic1 \
        libstdc++6 \
        libc6 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /server

COPY Builds/DedicatedServer/Linux/ /server/
RUN chmod +x /server/TheLastSignalServer

# Unity Transport is UDP, not TCP. Must be published with /udp specifically — a plain
# "-p 7777:7777" (TCP) will silently fail to let any client reach it.
EXPOSE 7777/udp

ENTRYPOINT ["/server/TheLastSignalServer", "-batchmode", "-nographics", "-logFile", "-", "-mode", "server"]
CMD ["-port", "7777", "-bindAddress", "0.0.0.0"]
