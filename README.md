# GridSchool world — server

The server for **the world**: the persistent multiplayer place built and operated by
[GridSchool](https://gridschool.org) students, live at [play.gridschool.org](https://play.gridschool.org).
You did not write this code. Someone before you did. That is the point.

.NET 8, raw WebSockets, Docker. The client is [gridschool-world-client](../../gridschool-world-client) (Unity, WebGL).

## Run it

```bash
dotnet run
# or
docker compose up
```

Server listens on :8080. `/ws` is the world. `/health` is the health check — and if the world is
empty, it will tell you the server is dead. It is not dead. That is issue GF-1. Welcome.

## Contributing

Read `CONTRIBUTING.md`. Every change ships through the ceremony: contract → PR → review → staging.
Missions live in [Issues](../../issues). Each has a `done_when`. A green build with an empty
failure log does not count.

## Who may touch what

| Ring | Surface | Who |
|---|---|---|
| Engine | tick, sockets, state, persistence | Students, full ceremony |
| Systems | chat, identity, zones | Students; one system per person |
| Art / scenes | Unity content, brand look | Maintainer |

The school itself (gridschool.org, the board, reviews) is not in this repo and cannot be broken from here. Break the world, not the school. Then write it down.
