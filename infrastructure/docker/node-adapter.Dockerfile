# fincore-node · adaptador de webhooks. Node 24 ejecuta TypeScript directamente (sin compilación).
# Contexto: raíz del repositorio.

FROM node:24-alpine AS base
WORKDIR /app
COPY integrations/fincore-node/package.json integrations/fincore-node/package-lock.json ./

FROM base AS test
RUN npm ci --no-audit --no-fund
COPY integrations/fincore-node/ .
CMD ["sh", "-c", "npx tsc --noEmit && npm test"]

FROM base AS runtime
ENV NODE_ENV=production
RUN npm ci --omit=dev --no-audit --no-fund
COPY integrations/fincore-node/src ./src
COPY integrations/fincore-node/scripts ./scripts
RUN mkdir -p /app/data && chown -R node:node /app/data
USER node
EXPOSE 5090
HEALTHCHECK --interval=10s --timeout=3s --retries=6 CMD wget -qO- http://127.0.0.1:5090/health || exit 1
CMD ["node", "src/server.ts"]
