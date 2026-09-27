# Nexbridge — B2B Integration Hub

A full-stack platform for managing B2B partner integrations: partner
onboarding, connected endpoints (REST/GraphQL/webhook/file-sync/EDI), a
real-time activity feed of webhook traffic, and role-based staff logins.

**Stack**
- **Frontend:** Angular 17 (standalone components) + TypeScript, Apollo GraphQL client (queries, mutations, and live subscriptions), responsive dark "control-tower" UI
- **Backend:** ASP.NET Core 8 (C# / .NET) + HotChocolate GraphQL server, JWT auth
- **Database:** MongoDB
- **Event bus:** Apache Kafka (KRaft, single node) — every webhook event is published to `webhook.events.v1` so other services can react independently of the Angular dashboard
- **Images:** Cloudinary (partner logos / uploaded assets)
- **Infra:** Docker + Docker Compose for local dev, AWS (ECS Fargate, ECR, Secrets Manager, S3) for production — see `aws/DEPLOY.md`

```
b2b-hub/
├── backend/              ASP.NET Core + GraphQL API + Kafka producer
├── frontend/             Angular app
├── services/             Kafka consumer services (Java/Kotlin/Python — see roadmap below)
├── aws/                  ECS task definition + deployment guide
├── docker-compose.yml    Full local stack (Mongo + Kafka + Kafka UI + backend + frontend)
└── .env.example          Copy to .env and fill in your keys
```

### Event streaming (Kafka)

Every webhook event that lands via `POST /api/webhooks/{integrationId}` is now published
to the Kafka topic **`webhook.events.v1`** (see `backend/Messaging/`), in addition to going
into Mongo and out over the existing GraphQL subscription. Kafka is best-effort: if it's
unreachable, the webhook still succeeds and the dashboard still updates — Kafka is an extra
stream for other services, not the primary path.

- **Bootstrap servers (in Docker):** `kafka:19092` (internal), exposed to your host machine as `localhost:9092`
- **Browse topics/messages:** http://localhost:8090 (Kafka UI, included in docker-compose)
- **Message contract:** `backend/Messaging/WebhookEventMessage.cs` — `eventId`, `integrationId`, `direction`, `eventType`, `statusCode`, `success`, `payload`, `errorMessage`, `receivedAt`

**Verify it end-to-end:**
```bash
docker compose up --build          # kafka + kafka-ui now start alongside mongo/backend/frontend
cd services/kafka-smoke-test-consumer
pip install -r requirements.txt
python consumer.py                 # leave this running
```
Then in another terminal, POST a test webhook (use a real integration id from the app):
```bash
curl -X POST http://localhost:8080/api/webhooks/<integrationId> \
     -H "Content-Type: application/json" \
     -d '{"eventType": "order.created", "payload": {"orderId": 123}}'
```
You should see the event printed by `consumer.py` within a second or two.

### Polyglot consumer services

The Kafka event bus above is the foundation everything else plugs into. Each service
below lives under `services/` and consumes `webhook.events.v1` independently — none of
them touch the .NET backend directly, they all read from Kafka (and Mongo, read-only).

| Service | Language | Job | Status |
|---|---|---|---|
| EDI Processor | Java (Spring Boot) | Consumes events for EDI integrations, parses X12 (ISA/GS/ST/SE/GE/IEA) payloads, stores results in `edi_processed_events` | ✅ Built |
| Notification/Retry Service | Kotlin (Ktor) | Retries failed outbound webhooks with backoff, raises an alert in `integration_alerts` after N consecutive failures | ✅ Built (`services/notification-service`, port 8082) |
| Analytics Service | Python (FastAPI) | Aggregates traffic stats (volume, success rate) per integration and as a daily time series; REST API for the dashboard | ✅ Built (`services/analytics-service`, port 8083) |

#### Testing the Notification/Retry Service

```bash
docker compose up --build     # also builds/starts notification-service on port 8082
curl http://localhost:8082/health
```
Send a few failing outbound events through the same webhook endpoint (`success: false`) for one
integration; after 3 in a row (`NOTIFICATION_ALERT_THRESHOLD`) a doc appears in Mongo's
`integration_alerts` collection and the service retries delivery to that integration's
`endpointUrl` with backoff (2s, 4s, 8s) before giving up.

#### Testing the Analytics Service

```bash
docker compose up --build     # also builds/starts analytics-service on port 8083
curl http://localhost:8083/analytics/overview
curl http://localhost:8083/analytics/timeseries?days=7
```
Every event on `webhook.events.v1` is aggregated into `integration_analytics` (running totals)
and `integration_analytics_daily` (per-day rollup) as it arrives.

`services/kafka-smoke-test-consumer/` is a throwaway verification script, not the real
analytics service — it'll be replaced once that's built.

#### Testing the EDI Processor

```bash
docker compose up --build     # also builds/starts edi-processor on port 8081
```

1. In the app, create (or reuse) an Integration with type **EDI** on some Partner, and
   copy its id.
2. Send it a valid X12 payload through the same REST webhook endpoint partners use:
   ```bash
   curl -X POST http://localhost:8080/api/webhooks/<ediIntegrationId> \
        -H "Content-Type: application/json" \
        -d '{"eventType": "850.received", "payload": "ISA*00*          *00*          *ZZ*SENDERID       *ZZ*RECEIVERID     *260927*1200*U*00401*000000905*0*T*:~GS*PO*SENDERID*RECEIVERID*20260927*1200*1*X*004010~ST*850*0001~BEG*00*NE*PO0001**20260927~SE*3*0001~GE*1*1~IEA*1*000000905~"}'
   ```
3. Check the parsed result:
   ```bash
   docker exec -it b2b-hub-mongo mongosh b2b_integration_hub --eval "db.edi_processed_events.find().pretty()"
   ```
   You should see one document with `parseSuccess: true` and the ISA/GS/ST fields filled in.
4. Sending the same event again is a no-op (idempotent on `eventId`). Sending an event for
   a non-EDI integration is silently ignored — nothing is written to `edi_processed_events`,
   by design. Tail `docker compose logs -f edi-processor` to watch it consume in real time.

---

## 1. Prerequisites

Install these once:

| Tool | Version | Check |
|---|---|---|
| Docker + Docker Compose | latest | `docker --version` |
| .NET SDK (only if running the backend outside Docker) | 8.0 | `dotnet --version` |
| Node.js + npm (only if running the frontend outside Docker) | 20.x | `node --version` |

You'll also need free accounts for:
- **MongoDB** — either install it locally, use the bundled Docker container (default), or create a free cluster at [mongodb.com/atlas](https://www.mongodb.com/atlas)
- **Cloudinary** — sign up free at [cloudinary.com](https://cloudinary.com), then copy your **Cloud name**, **API key**, and **API secret** from the dashboard
- **AWS account** — only needed for production deployment (see `aws/DEPLOY.md`); not required to run locally

---

## 2. Fastest path: run everything with Docker Compose

```bash
# 1. From the project root, copy the env template and fill in your Cloudinary + JWT values
cp .env.example .env
nano .env   # or any editor — paste your Cloudinary keys, set a long random JWT secret

# 2. Build and start Mongo + backend + frontend together
docker compose up --build
```

That single command:
- starts a MongoDB container and persists its data in a Docker volume
- builds and runs the .NET backend on **http://localhost:8080** (GraphQL at `/graphql`, health check at `/health`, Swagger UI in dev mode at `/swagger`)
- builds and runs the Angular frontend (served by nginx) on **http://localhost:4200**

To stop everything: `docker compose down` (add `-v` to also wipe the Mongo volume).

---

## 3. Create your first login

Open **http://localhost:4200/signup**, fill in the form (Admin role gives you
full access), and you're immediately logged in and dropped onto the dashboard
— no GraphQL Playground needed. `/login` and `/signup` are both plain pages
in the Angular app; behind the scenes they call the `login`/`register`
GraphQL mutations and store the returned JWT for you.

(The `register`/`login` mutations are still reachable directly at
**http://localhost:8080/graphql** if you ever need to script account
creation, but the app itself never requires you to open that page.)

---

## 4. Running the pieces individually (without Docker)

**Backend**
```bash
cd backend
dotnet restore
dotnet user-secrets set "MongoDb:ConnectionString" "mongodb://localhost:27017"
dotnet user-secrets set "Cloudinary:CloudName" "your_cloud_name"
dotnet user-secrets set "Cloudinary:ApiKey" "your_api_key"
dotnet user-secrets set "Cloudinary:ApiSecret" "your_api_secret"
dotnet user-secrets set "Jwt:SecretKey" "a-long-random-secret"
dotnet run
```
The API listens on `http://localhost:8080` (or whatever `ASPNETCORE_URLS` you set).

**Frontend**
```bash
cd frontend
npm install
npm start
```
Serves on `http://localhost:4200` and proxies GraphQL calls to
`environment.ts`'s `graphqlHttpUrl` (defaults to `http://localhost:8080/graphql`).

**MongoDB (if not using the Docker container)**
```bash
# macOS (Homebrew)
brew tap mongodb/brew && brew install mongodb-community && brew services start mongodb-community

# Ubuntu/Debian
sudo apt install -y mongodb

# Or just use MongoDB Atlas and paste the connection string into MongoDb:ConnectionString
```

---

## 5. What's included

- **Partners page** — create/list partner companies, change status (Pending → Active → Suspended → Offboarded), delete
- **Integrations page** — connect REST/GraphQL/Webhook/File-sync/EDI endpoints per partner, manage status, auto-generated API key reference
- **Dashboard** — network-status summary tiles plus a **live activity feed** that streams in over a GraphQL subscription (WebSocket) the instant a webhook event is recorded
- **REST webhook receiver** (`POST /api/webhooks/{integrationId}`) — so a partner's existing webhook sender can push events in without needing a GraphQL client; each accepted event flows straight into the live feed
- **JWT auth** with role-based accounts (Admin / Operator / Viewer) — sign up and sign in through `/signup` and `/login`, no GraphQL client needed
- **Responsive UI** — sidebar collapses to a top bar on mobile/tablet, all tables scroll horizontally on small screens, forms stack to a single column

---

## 6. Deploying to production

See **`aws/DEPLOY.md`** for the full walkthrough: pushing images to ECR,
storing secrets in AWS Secrets Manager, running the backend on ECS Fargate
behind an Application Load Balancer, and hosting the frontend via S3 +
CloudFront (or as a second Fargate service).

---

## 7. Troubleshooting

| Symptom | Fix |
|---|---|
| Frontend shows a blank dashboard / network errors | Check `environment.ts` points at the right `graphqlHttpUrl`/`graphqlWsUrl`, and that the backend's `Cors:AllowedOrigins` includes your frontend's origin |
| `dotnet run` fails to connect to Mongo | Confirm MongoDB is running and `MongoDb:ConnectionString` is correct (`mongodb://localhost:27017` for a local instance) |
| Logo/image upload fails | Double-check the three Cloudinary values — a wrong API secret is the most common cause |
| Live feed never updates | Make sure the WebSocket URL (`graphqlWsUrl`, `ws://` locally / `wss://` in production) is reachable — some proxies block WebSocket upgrades by default |
