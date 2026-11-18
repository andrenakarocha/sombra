# Sombra · Clima Service. Compila com o Maven no JDK 21 e roda no JRE 21 em Alpine, sem root (docs/PLANO.md §8.4).
FROM maven:3.9.11-eclipse-temurin-21-alpine AS build
WORKDIR /src
COPY pom.xml ./
RUN mvn -q -B dependency:go-offline
COPY src/ src/
RUN mvn -q -B package -DskipTests

FROM eclipse-temurin:21-jre-alpine
ARG COMMIT=dev
ARG CONSTRUIDO_EM=
ENV SOMBRA_COMMIT=${COMMIT} \
    SOMBRA_CONSTRUIDO_EM=${CONSTRUIDO_EM}
RUN addgroup -S sombra && adduser -S -G sombra -u 10001 sombra
WORKDIR /app
COPY --from=build /src/target/clima-service.jar app.jar
USER sombra
EXPOSE 8081
ENTRYPOINT ["java", "-XX:MaxRAMPercentage=75", "-jar", "app.jar"]
