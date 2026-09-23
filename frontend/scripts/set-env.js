// Runs before the production build (see vercel.json).
// Writes src/environments/environment.prod.ts from Vercel environment variables:
//   GRAPHQL_HTTP_URL  e.g. https://your-backend.onrender.com/graphql   (required)
//   GRAPHQL_WS_URL    e.g. wss://your-backend.onrender.com/graphql     (optional - derived from HTTP url if missing)
const fs = require("fs");
const path = require("path");

const http = (process.env.GRAPHQL_HTTP_URL || "").trim();
let ws = (process.env.GRAPHQL_WS_URL || "").trim();

if (!http) {
  console.warn(
    "[set-env] GRAPHQL_HTTP_URL is not set - keeping the placeholder URL in environment.prod.ts. " +
      "The site will build, but it will not reach your backend until you set this variable in Vercel and redeploy.",
  );
  process.exit(0);
}

// https://x -> wss://x , http://x -> ws://x
if (!ws) ws = http.replace(/^http/i, "ws");

const content =
  `export const environment = {\n` +
  `  production: true,\n` +
  `  graphqlHttpUrl: ${JSON.stringify(http)},\n` +
  `  graphqlWsUrl: ${JSON.stringify(ws)}\n` +
  `};\n`;

const target = path.join(
  __dirname,
  "..",
  "src",
  "environments",
  "environment.prod.ts",
);
fs.writeFileSync(target, content);
console.log("[set-env] environment.prod.ts written");
console.log("[set-env]   http:", http);
console.log("[set-env]   ws  :", ws);
