# Builds the Angular app and serves it with nginx, which also forwards /api to the backend.
# Build context: the frontend/angular folder (see docker-compose.yml).

FROM node:22-alpine AS build
WORKDIR /src
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npx ng build

FROM nginx:1.29-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/dist/angular/browser /usr/share/nginx/html
