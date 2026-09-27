# --- Build stage ---
FROM gradle:8.8-jdk17 AS build
WORKDIR /app
COPY build.gradle.kts settings.gradle.kts ./
COPY src ./src
RUN gradle --no-daemon clean jar

# --- Runtime stage ---
FROM eclipse-temurin:17-jre-jammy
WORKDIR /app
RUN useradd --system --create-home appuser
COPY --from=build /app/build/libs/notification-service.jar app.jar
USER appuser
EXPOSE 8082
HEALTHCHECK --interval=15s --timeout=5s --start-period=30s --retries=5 \
  CMD wget -qO- http://localhost:8082/health | grep -q '"status":"UP"' || exit 1
ENTRYPOINT ["java", "-jar", "app.jar"]
