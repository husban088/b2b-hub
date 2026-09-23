# Nexbridge — B2B Integration Hub

A full-stack platform for managing B2B partner integrations: partner
onboarding, connected endpoints (REST/GraphQL/webhook/file-sync/EDI), a
real-time activity feed of webhook traffic, and role-based staff logins.

**Stack**
- **Frontend:** Angular 17 (standalone components) + TypeScript, Apollo GraphQL client (queries, mutations, and live subscriptions), responsive dark "control-tower" UI
- **Backend:** ASP.NET Core 8 (C# / .NET) + HotChocolate GraphQL server, JWT auth
- **Database:** MongoDB
- **Images:** Cloudinary (partner logos / uploaded assets)
- **Infra:** Docker + Docker Compose for local dev, AWS (ECS Fargate, ECR, Secrets Manager, S3) for production — see `aws/DEPLOY.md`

```
b2b-hub/
├── backend/            ASP.NET Core + GraphQL API
├── frontend/            Angular app
├── aws/                 ECS task definition + deployment guide
├── docker-compose.yml    Full local stack (Mongo + backend + frontend)
└── .env.example          Copy to .env and fill in your keys
```

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

The dashboard requires a logged-in user. Register one through GraphQL —
open **http://localhost:8080/graphql** (Banana Cake Pop / GraphQL IDE ships
with HotChocolate) and run:

```graphql
mutation {
  register(input: {
    fullName: "Your Name"
    email: "you@example.com"
    password: "a-strong-password"
    role: ADMIN
  }) {
    id
    email
  }
}
```

Then open **http://localhost:4200**, sign in with that email/password, and
you'll land on the live dashboard.

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
- **JWT auth** with role-based accounts (Admin / Operator / Viewer)
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
