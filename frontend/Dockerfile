# Development-server container for local iteration (per technical-architecture.md
# §4 — the production/static build is a later CI/CD concern).
FROM node:22-alpine
WORKDIR /app

# Install dependencies first for better layer caching. `npm install` (not
# `npm ci`) is used deliberately: the committed lockfile is generated on the
# host OS and omits Linux-only optional native deps, which `npm ci` rejects.
COPY package.json package-lock.json ./
RUN npm install

COPY . .

EXPOSE 5173
# --host makes Vite listen on 0.0.0.0 so the port is reachable from the host.
CMD ["npm", "run", "dev", "--", "--host"]
