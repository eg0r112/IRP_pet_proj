# IRP Pet Project

Incident Response Platform — .NET 9 pet-проект.

Документация и быстрый старт: [irp_pet/README.md](irp_pet/README.md)

```powershell
cd irp_pet
docker compose up -d
dotnet run --launch-profile http
```

Секреты (Telegram, Jira, пароль БД) — в `irp_pet/appsettings.Development.json` (не в git). Скопируй из `appsettings.json` и заполни свои значения.
