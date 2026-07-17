FROM dhi.io/dotnet:10-sdk AS build

# TARGETARCH is automatically provided by Docker when using --platform.
# Falls back to x64 if not set.
ARG TARGETARCH=x64

WORKDIR /source

# Copy project files and restore dependencies (cached layer)
COPY DocMostMcp.Server/DocMostMcp.Server.csproj .
RUN dotnet restore -a $TARGETARCH

# Copy the rest of the source and publish as a self-contained single file
COPY DocMostMcp.Server/ .
RUN dotnet publish \
    -c Release \
    -a $TARGETARCH \
    --self-contained true \
    /p:PublishSingleFile=true \
    -o /app

# -----------------------
# Stage 2: Runtime
# -----------------------
# Docker Hardened Image (DHI): minimal, near-zero CVEs, Debian 13-based.
FROM dhi.io/dotnet:10

WORKDIR /app
COPY --from=build /app .

# DHI runtime image already runs as non-root (UID 65532)

# In container environments, always use HTTP transport
ENV DOCMOST_MCP_TRANSPORT=http

# Default MCP HTTP port (overridable via DOCMOST_MCP_PORT)
EXPOSE 3001

ENTRYPOINT ["./DocMostMcp.Server"]
