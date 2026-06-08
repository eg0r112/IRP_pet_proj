# IRP — гайд для команды

Практичная документация для разработчиков. API описан в **Swagger** — http://localhost:5185/swagger

---

## Локальный запуск

```powershell
cd irp_pet
docker compose up -d
dotnet run --launch-profile http
```

При старте: `MigrateAsync` + `DbSeeder` (пользователи, сервисы, on-call смены; **5 демо-инцидентов**, если таблица `incidents` пустая).

| Сервис | URL / порт |
|--------|------------|
| API / Swagger | http://localhost:5185/swagger |
| Admin UI | http://localhost:5185/admin |
| Health | http://localhost:5185/health |
| Metrics | http://localhost:5185/metrics |
| PostgreSQL | localhost:5432 |
| Redis | localhost:6379 |
| RabbitMQ AMQP | localhost:5672 |
| RabbitMQ UI | http://localhost:15672 (guest/guest) |
| Prometheus | http://localhost:9090 |
| Grafana | http://localhost:3000 (admin/admin) |
| Jaeger | http://localhost:16686 |

---

## Учётки и ключи (seed)

| Роль | Email | Пароль |
|------|-------|--------|
| Admin | vedromakaron@gmail.com | 123456 |
| On-call | oncall@irp.local | 123456 |

- **API Key** (заголовок `X-Api-Key`): `dev-api-key-change-me` — только для `POST /alerts`
- **Сервисы в каталоге:** `orders-api`, `payments-api`

---

## Структура кода

```
Controllers/          HTTP, версии v1/v2
Application/          MediatR commands/queries + FluentValidation
services/             AlertService, IncidentService, OnCallService, AuthService
Infrastructure/       Telegram, Jira, outbox processor, DI, metrics
Background/           OutboxDispatcher, EscalationWorker, TelegramBotWorker
Data/                 AppDbContext, DbSeeder
Models/               сущности EF
Messaging/            MassTransit consumer
wwwroot/admin/        HTML-админка (on-call, пользователи)
```

---

## API: две версии

| | v1 | v2 |
|---|----|----|
| Alerts | `POST /api/v1/alerts` | `POST /api/v2/alerts` (+ `receivedAtUtc` в ответе) |
| Incidents list | без пагинации | `page`, `pageSize`, `totalCount` |

Полные схемы — в Swagger.

---

## Конфигурация (`appsettings.json`)

| Секция | Зачем |
|--------|-------|
| `ConnectionStrings` | PostgreSQL, Redis |
| `RabbitMq:Enabled` | `false` — outbox обрабатывается локально (удобно для тестов без Docker) |
| `Telegram:Enabled` / `BotPollingEnabled` / `InteractiveButtons` | push, polling, кнопки в чате |
| `Telegram:NotifyOn` | на какие события слать push (Created, Escalated, …) |
| `Jira:Enabled` | интеграция с Jira |
| `OpenTelemetry:OtlpEndpoint` | трейсы в Jaeger |

Секреты в проде — через env / user secrets, не коммитить реальные токены.

---

## Тесты

```powershell
cd ..
dotnet test Pet-project.sln
```