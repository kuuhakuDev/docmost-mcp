FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build

RUN apk add --no-cache clang build-base zlib-dev

WORKDIR /source

COPY DocMostMcp.Server/DocMostMcp.Server.csproj .
RUN dotnet restore

COPY DocMostMcp.Server/ .
RUN dotnet publish \
    -c Release \
    -r linux-musl-x64 \
    -o /app

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-alpine AS runtime

WORKDIR /app
COPY --from=build /app .

ENV DOCMOST_MCP_TRANSPORT=http
EXPOSE 3001

ENTRYPOINT ["/app/DocMostMcp.Server"]
