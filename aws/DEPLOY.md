# Deploying to AWS

This platform ships as two Docker images (backend, frontend) plus MongoDB.
The recommended production layout:

```
Route 53 → CloudFront (frontend static site, S3 or nginx container)
         → Application Load Balancer → ECS Fargate service (backend, port 8080)
                                       → MongoDB Atlas (recommended) or DocumentDB
Cloudinary → image storage (external SaaS, no AWS setup needed)
S3 → optional large-file / backup storage, referenced by the backend's AWS SDK client
Secrets Manager → Mongo connection string, Cloudinary keys, JWT secret
```

## 1. Container registry (ECR)

```bash
aws ecr create-repository --repository-name b2b-integration-hub-backend
aws ecr create-repository --repository-name b2b-integration-hub-frontend

aws ecr get-login-password --region <REGION> | \
  docker login --username AWS --password-stdin <ACCOUNT_ID>.dkr.ecr.<REGION>.amazonaws.com

docker build -t b2b-integration-hub-backend ./backend
docker tag b2b-integration-hub-backend:latest <ACCOUNT_ID>.dkr.ecr.<REGION>.amazonaws.com/b2b-integration-hub-backend:latest
docker push <ACCOUNT_ID>.dkr.ecr.<REGION>.amazonaws.com/b2b-integration-hub-backend:latest

docker build -t b2b-integration-hub-frontend ./frontend
docker tag b2b-integration-hub-frontend:latest <ACCOUNT_ID>.dkr.ecr.<REGION>.amazonaws.com/b2b-integration-hub-frontend:latest
docker push <ACCOUNT_ID>.dkr.ecr.<REGION>.amazonaws.com/b2b-integration-hub-frontend:latest
```

## 2. Database (MongoDB)

Easiest path: create a free/shared **MongoDB Atlas** cluster, whitelist your
VPC's NAT gateway IP, and copy the connection string into Secrets Manager.
(DocumentDB is a same-API AWS-native alternative if you'd rather stay fully
inside AWS — the backend's `MongoDb__ConnectionString` works with either.)

## 3. Store secrets

```bash
aws secretsmanager create-secret --name b2b-hub/mongo-connection-string --secret-string "mongodb+srv://..."
aws secretsmanager create-secret --name b2b-hub/cloudinary-cloud-name --secret-string "your_cloud_name"
aws secretsmanager create-secret --name b2b-hub/cloudinary-api-key --secret-string "your_api_key"
aws secretsmanager create-secret --name b2b-hub/cloudinary-api-secret --secret-string "your_api_secret"
aws secretsmanager create-secret --name b2b-hub/jwt-secret --secret-string "$(openssl rand -base64 48)"
```

## 4. ECS Fargate service (backend)

1. Fill in `<ACCOUNT_ID>` / `<REGION>` in `aws/ecs-task-definition.json`.
2. Register it: `aws ecs register-task-definition --cli-input-json file://aws/ecs-task-definition.json`
3. Create a cluster: `aws ecs create-cluster --cluster-name b2b-integration-hub`
4. Create a service behind an Application Load Balancer, target group health
   check path `/health`, container port `8080`.
5. Point the ALB at an ACM certificate for HTTPS, and add a Route 53 record
   (e.g. `api.your-domain.com`) pointing at the ALB.

## 5. Frontend hosting

Two options — pick one:

- **Static hosting (cheapest, recommended):** run `npm run build:prod` locally
  or in CI, upload `dist/b2b-integration-hub/browser` to an S3 bucket, and
  serve it through CloudFront with an Angular-routing-friendly error page
  (redirect 403/404 → `/index.html`).
- **Container hosting:** deploy the `frontend` image (nginx-based) as a
  second small ECS Fargate service behind the same ALB, or behind its own
  CloudFront distribution pointed at an ALB origin.

Before building, set `src/environments/environment.prod.ts` to your real
`api.your-domain.com` GraphQL URL.

## 6. Wire CORS

Update the backend's `Cors:AllowedOrigins` (env var `Cors__AllowedOrigins__0`)
to your deployed frontend's exact origin, e.g. `https://app.your-domain.com`.

## 7. GitHub Actions (optional CI/CD)

A minimal pipeline: on push to `main`, build both images, push to ECR, then
call `aws ecs update-service --force-new-deployment` for the backend service
and invalidate the CloudFront distribution for the frontend.
