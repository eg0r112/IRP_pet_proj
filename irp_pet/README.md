# IRP — Incident Response Platform

Pet-проект: платформа реагирования на инциденты. Мониторинг шлёт алерт → создаётся или обновляется инцидент → дежурный получает уведомление в Telegram, принимает и закрывает через бота или API.

---

## Быстрый старт

```powershell
cd irp_pet
docker compose up -d
dotnet run --launch-profile http
```

| Сервис | URL | Логин |
|--------|-----|-------|
| API / Swagger | http://localhost:5185/swagger | — |
| Admin UI | http://localhost:5185/admin | см. seed ниже |
| Health | http://localhost:5185/health | — |
| Metrics (для Prometheus) | http://localhost:5185/metrics | — |
| Grafana | http://localhost:3000 | admin / admin |
| Prometheus | http://localhost:9090 | — |
| Jaeger (трейсы) | http://localhost:16686 | — |
| RabbitMQ UI | http://localhost:15672 | guest / guest |

**Docker Compose** (без веб-UI): PostgreSQL `localhost:5432`, Redis `localhost:6379`, RabbitMQ AMQP `localhost:5672`.

**Seed:** admin `vedromakaron@gmail.com` / `123456` · on-call `oncall@irp.local` / `123456` · API Key `dev-api-key-change-me`

Подробности для разработки — в [README_FOR_TEAM.md](README_FOR_TEAM.md).

---

## Как это работает

1. Мониторинг шлёт `POST /api/v1/alerts` (API Key).
2. API создаёт или обновляет инцидент в PostgreSQL (один открытый инцидент на пару `serviceKey` + `fingerprint`).
3. В ту же транзакцию пишется запись в outbox.
4. Фоновый worker забирает outbox и публикует событие в RabbitMQ.
5. Consumer шлёт уведомление в Telegram (и в Jira, если включено).
6. Дежурный принимает/закрывает инцидент через бота или API (JWT).

**Зачем outbox:** если RabbitMQ лежит, инцидент уже в БД — событие не потеряется, worker отправит позже.

**Зачем `rowVersion`:** у каждого инцидента номер версии. При ack/resolve клиент передаёт версию, которую видел. Если за это время кто-то уже изменил инцидент — API отвечает `409`, нужно обновить данные и повторить. Чтобы два дежурных не перетёрли действия друг друга.

---

## Стек

**Backend:** .NET 9, ASP.NET Core, MediatR, FluentValidation, AutoMapper, EF Core, PostgreSQL

**Очереди и фон:** Outbox, RabbitMQ, MassTransit, `BackgroundService` (outbox dispatcher, escalation, Telegram polling)

**Кэш:** Redis — dedup алертов и idempotency keys

**Безопасность:** JWT (admin / on-call), API Key (мониторинг), RBAC по ролям

**API:** Swagger / OpenAPI, версионирование (v1 / v2), ProblemDetails, optimistic concurrency (`rowVersion`)

**Интеграции:** Telegram Bot API, Jira Cloud (опционально)

**Мониторинг и логи:** Serilog, CorrelationId, OpenTelemetry → Jaeger, Prometheus, Grafana, health checks

**Инфраструктура:** Docker Compose (Postgres, Redis, RabbitMQ, Prometheus, Grafana, Jaeger), GitHub Actions CI

**Тесты:** xUnit, FluentAssertions, Bogus, WebApplicationFactory, Testcontainers

---

## Тесты

```powershell
cd ..
dotnet test Pet-project.sln
```
