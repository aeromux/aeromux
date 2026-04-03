# Aeromux Docker Image
# Multi-arch Dockerfile for linux/arm64 and linux/amd64
#
# This Dockerfile copies pre-built binaries into the runtime image.
# The binary is built outside Docker by build.sh — nothing is compiled here.

FROM debian:bookworm-slim

ARG TARGETARCH
ARG VERSION

# OCI labels — links image to repository and displays metadata on GHCR
LABEL org.opencontainers.image.title="Aeromux" \
      org.opencontainers.image.description="Multi-SDR Mode S and ADS-B demodulator and decoder" \
      org.opencontainers.image.version="${VERSION}" \
      org.opencontainers.image.source="https://github.com/aeromux/aeromux" \
      org.opencontainers.image.url="https://github.com/aeromux/aeromux" \
      org.opencontainers.image.licenses="GPL-3.0-or-later" \
      org.opencontainers.image.vendor="Nandor Toth"

# Install runtime dependencies
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        librtlsdr0 \
        libicu72 \
        ca-certificates \
        gosu && \
    rm -rf /var/lib/apt/lists/*

# Create non-root user and directories
RUN groupadd --system aeromux && \
    useradd --system --gid aeromux --home /var/lib/aeromux --no-create-home aeromux && \
    mkdir -p /etc/aeromux /var/lib/aeromux /var/log/aeromux && \
    chown aeromux:aeromux /var/lib/aeromux /var/log/aeromux

# Copy binary (architecture-specific via TARGETARCH: arm64 or amd64)
COPY binaries/${TARGETARCH}/aeromux /usr/bin/aeromux
RUN chmod 755 /usr/bin/aeromux

# Copy default configuration
COPY aeromux.yaml /etc/aeromux/aeromux.yaml

# Copy entrypoint
COPY entrypoint.sh /docker-entrypoint.sh
RUN chmod 755 /docker-entrypoint.sh

# Volumes for persistent data
VOLUME ["/var/lib/aeromux", "/var/log/aeromux"]

# Expose default ports (Beast, API, MLAT)
EXPOSE 30005 8080 30104

# .NET single-file bundle extraction directory — must be writable by the
# non-root user regardless of volume mount permissions on /var/lib/aeromux.
ENV DOTNET_BUNDLE_EXTRACT_BASE_DIR=/tmp/dotnet-bundle

ENTRYPOINT ["/docker-entrypoint.sh"]
CMD ["aeromux", "daemon", "--config", "/etc/aeromux/aeromux.yaml"]
