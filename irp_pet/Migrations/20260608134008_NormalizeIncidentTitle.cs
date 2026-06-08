using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace irp_pet.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeIncidentTitle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Убрать устаревший префикс [serviceKey] — serviceKey хранится в ServiceId.
            migrationBuilder.Sql(
                """
                UPDATE incidents
                SET "Title" = regexp_replace("Title", '^\[[^\]]+\]\s*', '')
                WHERE "Title" ~ '^\[';
                """);

            // Перевести типовые английские тексты из демо/тестов в русские формулировки.
            migrationBuilder.Sql(
                """
                UPDATE incidents
                SET "Title" = CASE trim("Title")
                    WHEN 'integration test alert' THEN 'Сбой интеграционного теста: сервис недоступен'
                    WHEN 'idempotency test' THEN 'Повторный алерт: проверка идемпотентности'
                    WHEN 'concurrency test' THEN 'Конфликт версий при одновременном изменении'
                    WHEN 'testcontainers pipeline' THEN 'Ошибка HTTP 500 при проверке health'
                    WHEN 'rabbitmq consumer test' THEN 'Сообщение не доставлено в очередь RabbitMQ'
                    WHEN 'HTTP 500 spike' THEN 'Резкий рост ошибок HTTP 500'
                    WHEN 'HTTP 500' THEN 'Ошибка HTTP 500'
                    ELSE "Title"
                END
                WHERE trim("Title") IN (
                    'integration test alert',
                    'idempotency test',
                    'concurrency test',
                    'testcontainers pipeline',
                    'rabbitmq consumer test',
                    'HTTP 500 spike',
                    'HTTP 500'
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Обратное преобразование не выполняется — исходные английские формулировки не восстанавливаются.
        }
    }
}
