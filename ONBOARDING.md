# Onboarding — server track (target: 45 minutes on a clean machine)

You need: git, .NET 8 SDK, Docker Desktop. Nothing else. Unity is NOT required for the
server track — client work is a separate, later track.

## 1. Tools (skip what you have)

- .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0 — verify: `dotnet --version` → 8.x
- Docker Desktop: https://docs.docker.com/desktop/ — verify: `docker --version`
- git: `git --version`

## 2. Run the world locally

```bash
git clone https://github.com/Grid-School/gridschool-world-server.git
cd gridschool-world-server
dotnet run
```

You should see `[WebSocketHandler] Handler initialized.` The server is on http://localhost:8080.

Check health with the world empty:

```bash
curl -i http://localhost:8080/health
```

In Development it says OK. Now run it the way production runs it:

```bash
docker compose up --build
# in another terminal:
curl -i http://localhost:8080/health
```

**503 Service Unavailable.** The world reports itself dead because nobody is inside it.
Production Docker would restart this server in a loop all night. You just reproduced GF-1
— the first mission — in your first 20 minutes. Nothing is broken on your machine.

## 3. Walk into the world

Open the hosted web client (link in Discord `#ops`) and set the server URL to
`ws://localhost:8080/ws`. You should spawn on the grid. Open a second browser tab: two of you.
Move in one tab, watch the other.

If the hosted client is not up yet, the maintainer will screen-share this step in Session 1.

## 4. Prove it ran (lights your first node)

Paste into a gist or `#ship`:

1. The `dotnet run` startup line.
2. Both curl outputs (dev OK, docker 503) with one sentence: why they differ.
3. A screenshot of your avatar in the world (or both tabs).

Attach the URL to **It runs** on your board. That is your first light.

## 5. If you get stuck

30 minutes of honest effort, then post in `#asks` with: what you ran, what you expected,
what happened, what you tried. That format is not bureaucracy — it is the job.
