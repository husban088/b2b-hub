# ---------- Build stage ----------
FROM node:20-alpine AS build
WORKDIR /app

# Backend URLs baked into the production bundle.
# Defaults point at the local docker-compose backend; override with --build-arg for AWS/production.
ARG GRAPHQL_HTTP_URL=http://localhost:8080/graphql
ARG GRAPHQL_WS_URL=ws://localhost:8080/graphql

COPY package.json ./
RUN npm install

COPY . .

# environment.prod.ts ships with a placeholder domain - replace it with the real URLs before building.
RUN sed -i "s|graphqlHttpUrl: '.*'|graphqlHttpUrl: '${GRAPHQL_HTTP_URL}'|; s|graphqlWsUrl: '.*'|graphqlWsUrl: '${GRAPHQL_WS_URL}'|" src/environments/environment.prod.ts

RUN npm run build:prod

# ---------- Runtime stage ----------
FROM nginx:1.27-alpine AS runtime

COPY --from=build /app/dist/b2b-integration-hub/browser /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD wget --no-verbose --tries=1 --spider http://localhost:80 || exit 1

CMD ["nginx", "-g", "daemon off;"]