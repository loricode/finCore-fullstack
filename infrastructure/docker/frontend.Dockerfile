# FinCore web · Angular compilado y servido por nginx, que además hace de proxy hacia la API.
# Contexto: raíz del repositorio.

FROM node:24-alpine AS build
WORKDIR /app
COPY frontend/fincore-angular/package.json frontend/fincore-angular/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY frontend/fincore-angular/ .
RUN npx ng build --configuration production

FROM build AS test
CMD ["npm", "run", "test:ci"]

FROM nginx:1.27-alpine AS runtime
COPY infrastructure/docker/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/fincore-angular/browser /usr/share/nginx/html
EXPOSE 80
