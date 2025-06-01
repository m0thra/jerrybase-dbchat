﻿using DBChatPro.Models;
using Microsoft.Data.SqlClient;
using MySqlConnector;
using Npgsql;

namespace DBChatPro
{
    public class PostgresDatabaseService : IDatabaseService
    {
        public async Task<List<List<string>>> GetDataTable(AIConnection conn, string sqlQuery)
        {
            var rows = new List<List<string>>();
            var dataSource = NpgsqlDataSource.Create(conn.ConnectionString);

            await using (var cmd = dataSource.CreateCommand(sqlQuery))
            
            await using (var reader = await cmd.ExecuteReaderAsync())
            {
                int count = 0;
                bool headersAdded = false;
                while (await reader.ReadAsync())
                {
                    var cols = new List<string>();
                    var headerCols = new List<string>();
                    if (!headersAdded)
                    {
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            headerCols.Add(reader.GetName(i).ToString());
                        }
                        headersAdded = true;
                        rows.Add(headerCols);
                    }

                    for (int i = 0; i <= reader.FieldCount - 1; i++)
                    {
                        try
                        {
                            cols.Add(reader.GetValue(i).ToString());
                        }
                        catch
                        {
                            cols.Add("DataTypeConversionError");
                        }
                    }
                    rows.Add(cols);
                }
            }

            return rows;
        }

        public async Task<DatabaseSchema> GenerateSchema(AIConnection conn)
        {
            return await GenerateSchema(conn, null);
        }


        public async Task<DatabaseSchema> GenerateSchema(AIConnection conn, List<string>? excludedTables = null)
        {
            var dbSchema = new DatabaseSchema() { SchemaRaw = new List<string>(), SchemaStructured = new List<TableSchema>() };
            List<KeyValuePair<string, string>> rows = new();

            var pairs = conn.ConnectionString.Split(";");
            var database = pairs.Where(x => x.Contains("Database")).FirstOrDefault().Split("=").Last();

            // Build exclusion filter for SQL
            string exclusionFilter = "";
            if (excludedTables != null && excludedTables.Count > 0)
            {
                var excludedList = string.Join(", ", excludedTables.Select(t => $"'{t}'"));
                exclusionFilter = $"AND table_name NOT IN ({excludedList})";
            }

            string sqlQuery = $@"SELECT 
                                    table_name, 
                                    column_name 
                                FROM 
                                    information_schema.columns 
                                WHERE 
                                    table_catalog = '{database}'
                                    AND table_schema = 'public'
                                    {exclusionFilter}
                                ORDER BY 
                                    table_name, 
                                    column_name;";

            var dataSourceBuilder = new NpgsqlDataSourceBuilder(conn.ConnectionString);
            var dataSource = dataSourceBuilder.Build();

            var connection = await dataSource.OpenConnectionAsync();

            await using (var cmd = new NpgsqlCommand(sqlQuery, connection))
            await using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    rows.Add(new KeyValuePair<string, string>(reader.GetString(0), reader.GetString(1)));
                }
            }

            var groups = rows.GroupBy(x => x.Key);

            foreach (var group in groups)
            {
                dbSchema.SchemaStructured.Add(new TableSchema() { TableName = group.Key, Columns = group.Select(x => x.Value).ToList() });
            }

            var textLines = new List<string>();

            foreach (var table in dbSchema.SchemaStructured)
            {
                var schemaLine = $"- {table.TableName} (";

                foreach (var column in table.Columns)
                {
                    schemaLine += column + ", ";
                }

                schemaLine += ")";
                schemaLine = schemaLine.Replace(", )", " )");

                Console.WriteLine(schemaLine);
                textLines.Add(schemaLine);
            }

            dbSchema.SchemaRaw = textLines;

            return dbSchema;
        }
    }
}
