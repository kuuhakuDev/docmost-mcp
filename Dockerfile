FROM dhi.io/dotnet:10-sdk AS build

WORKDIR /source

COPY DocMostMcp.Server/DocMostMcp.Server.csproj .
RUN dotnet restore

COPY DocMostMcp.Server/ .
RUN dotnet publish \
    -c Release \
    --self-contained false \
    /p:PublishSingleFile=false \
    /p:PublishSelfContained=false \
    -o /app

FROM dhi.io/aspnetcore:10

WORKDIR /app
COPY --from=build /app .

ENV DOCMOST_MCP_TRANSPORT=http
EXPOSE 3001

ENTRYPOINT ["dotnet", "DocMostMcp.Server.dll"]
