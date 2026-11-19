# Sombra · painel web. Build no Node 22 e o estático servido pelo nginx sem root (o mesmo nginx.conf da VM, adaptado).
FROM node:22-alpine AS build
WORKDIR /src
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY . .
COPY --from=design tokens.css /design/tokens.css
ARG COMMIT=dev
ENV SOMBRA_COMMIT=${COMMIT}
RUN npm run build

FROM nginxinc/nginx-unprivileged:1.29-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/dist /usr/share/nginx/html
EXPOSE 5173
