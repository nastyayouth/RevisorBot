# Runbook — Revisor Bot

Этот документ содержит практические команды и шаги для диагностики и поддержки сервиса Revisor Bot
(AWS Copilot + ECS Fargate + Aurora PostgreSQL + Telegram Bot).
---
## Проверка состояния сервиса
Статус сервиса
```
copilot svc status --name revisor-service --env prod
```

Ожидаемо:

- Running: 1/1

- HTTP Health: HEALTHY

- Container Health: HEALTHY

Логи сервиса
```
copilot svc logs --name revisor-service --env prod
```
Последние N минут:
```
copilot svc logs --name revisor-service --env prod --since 5m
```
---
## Подключение к контейнеру (ECS exec)
Зайти в контейнер
copilot svc exec --name revisor-service --env prod


Проверка переменных окружения:
```
env | sort
```

Проверка connection string:
```
env | grep ConnectionStrings
```
---

## Проверка доступности базы данных
Установить psql (если нужно)
```
apt-get update && apt-get install -y postgresql-client
```
Подключиться к Aurora PostgreSQL
```
psql "host=<RDS_ENDPOINT> port=5432 user=postgres dbname=postgres sslmode=require"
```
Список баз данных
```
\l
```

Ожидаемо:

- postgres

- rdsadmin

- revisorDB

Подключение к рабочей БД
```
\c revisorDB
```
Проверка таблиц (EF Core)
```
\dt
```
Ожидаемо:

- Users

- Products

- __EFMigrationsHistory

Проверка данных
```
SELECT COUNT(*) FROM "Products";
SELECT COUNT(*) FROM "Users";
```
---

## Проверка миграций EF Core

Миграции применяются при старте приложения:

db.Database.Migrate();


Проверка в БД:
```
SELECT * FROM "__EFMigrationsHistory";
```
---
## Telegram Bot — Update Mode (configurable)

Режим доставки апдейтов теперь выбирается через конфиг `Telegram:UsePolling`.

Активный сервис
- `true`  → работает `TelegramPollingService`
- `false` → polling не регистрируется, используется webhook endpoint `/telegram/update`

Актуальная логика в `Program.cs`:
```
var usePolling = builder.Configuration.GetValue<bool?>("Telegram:UsePolling") ?? true;
if (usePolling)
{
    builder.Services.AddHostedService<TelegramPollingService>();
}
```
Проверка режима в логах приложения:
```
Telegram update mode: Polling
Telegram update mode: Webhook
```
### Polling mode
```
copilot svc logs --name revisor-service --env prod | Select-String Telegram
```

Ожидаемо:

- TelegramPollingService
- Update received: Message

---

### Webhook mode
Проверка webhook:
```
irm "https://api.telegram.org/bot<TOKEN>/getWebhookInfo"
```

Если polling выключен и webhook включён, поле `url` должно содержать endpoint.

---

## Health Checks

ALB health
```
GET /health
```
Глубокий DB check
```
GET /health/db
```


Ожидаемо:
```
{
    "status": "Healthy",
        "entries": {
            "db": {
                "status": "Healthy"
            }
    }
}
```
---

## Secrets (Copilot)
Список секретов
```
aws secretsmanager list-secrets --region us-east-2
```

Используемый секрет
```
ConnectionStrings__Postgres
```


Проверка в контейнере:
```
env | grep ConnectionStrings
```
---

## Частые проблемы
###  DB unhealthy

Причины:

- неверный connection string

- БД не существует

- Security Group не разрешает 5432

- миграции не применились

Проверка:
```
\l
\dt
```
### Telegram не отвечает

- polling не запущен

- webhook активен одновременно

- токен неверный

Проверка:
```
copilot svc logs --name revisor-service --env prod | Select-String Telegram
```
---

## Полезные команды AWS
### Проверка RDS
```
aws rds describe-db-clusters --region us-east-2
```
### Security Groups
```
aws ec2 describe-security-groups --group-ids <SG_ID> --region us-east-2
```