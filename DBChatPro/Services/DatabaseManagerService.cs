using DBChatPro.Models;
using Microsoft.Data.SqlClient;
using Oracle.ManagedDataAccess.Types;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;

namespace DBChatPro
{
    public class DatabaseManagerService(
        MySqlDatabaseService mySqlDb, 
        SqlServerDatabaseService msSqlDb, 
        PostgresDatabaseService postgresDb, 
        OracleDatabaseService oracleDb) : IDatabaseService
    {
        public async Task<List<List<string>>> GetDataTable(AIConnection conn, string sqlQuery)
        {
            switch (conn.DatabaseType)
            {
                case "MSSQL":
                    return await msSqlDb.GetDataTable(conn, sqlQuery);
                case "MYSQL":
                    return await mySqlDb.GetDataTable(conn, sqlQuery);
                case "POSTGRESQL":
                    return await postgresDb.GetDataTable(conn, sqlQuery);
                case "ORACLE":
                    return await oracleDb.GetDataTable(conn, sqlQuery);
            }

            return null;
        }

        public async Task<DatabaseSchema> GenerateSchema(AIConnection conn)
        {
            switch (conn.DatabaseType)
            {
                case "MSSQL":
                    return await msSqlDb.GenerateSchema(conn);
                case "MYSQL":
                    return await mySqlDb.GenerateSchema(conn);
                case "POSTGRESQL":
                    var excludedTables = new List<string>
                    {
                        "action_text_rich_texts",
                        "active_storage_attachments",
                        "active_storage_blobs",
                        "active_storage_variant_records",
                        "ar_internal_metadata",
                        "audits",
                        "friendly_id_slugs",
                        "help_pages",
                        "jobs",
                        "notices",
                        "saved_searches",
                        "scheduled_tasks",
                        "schema_migrations",
                        "sessions",
                        "term_explanations",
                        "user_events",
                        "users",
                        "whats_news"
                    };
                    return await postgresDb.GenerateSchema(conn, excludedTables);
                case "ORACLE":
                    return await oracleDb.GenerateSchema(conn);
            }

            return new() { SchemaStructured = new List<TableSchema>(), SchemaRaw = new List<string>() };
        }
    }
}
