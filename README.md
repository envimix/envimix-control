# Envimix Control

[![CI](https://github.com/envimix/envimix-control/actions/workflows/ci.yml/badge.svg)](https://github.com/envimix/envimix-control/actions/workflows/ci.yml)
[![Docker image](https://img.shields.io/docker/v/envimix/envimix-control?label=Docker%20Hub)](https://hub.docker.com/r/envimix/envimix-control)

Envimix Control connects via GBXRemote to a Trackmania 2 ENVIMIX server and submits player best ghosts, validation replays, and Envimania session replays to online services.

## How it works

The controller authenticates with GBXRemote, enables Envimania session recording when needed, and listens for player-finish and end-race callbacks. Replay uploads from a player finish are submitted in parallel. Successfully submitted local replays are deleted, unsuccessful submissions leave their files in place.

## Docker images

Version tags are published for `linux/amd64` and `linux/arm64` to both registries:

```text
bigbang1112/envimix-control:<version>
ghcr.io/envimix/envimix-control:<version>
```

## Docker setup

The container must be able to reach the server's XML-RPC.

```sh
docker run -d \
  -e EMC_SERVER_IP=envimix \
  -e EMC_SERVER_PORT=5000 \
  -e EMC_CONTROLLER_CODE=your-controller-code \
  envimix/envimix-control:latest
```

## Environment variables

| Variable | Default | Description |
| --- | --- | --- |
| `EMC_CONTROLLER_CODE` | Required | Controller code generated from the Envimix server page |
| `EMC_SERVER_IP` | `127.0.0.1` | Server address |
| `EMC_SERVER_PORT` | `5000` | XML-RPC port |
| `EMC_SUPERADMIN_LOGIN` | `SuperAdmin` | SuperAdmin login |
| `EMC_SUPERADMIN_PASSWORD` | `SuperAdmin` | SuperAdmin password |
