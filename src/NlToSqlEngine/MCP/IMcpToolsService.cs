using System.Collections.Generic;
using System.Threading.Tasks;

namespace NlToSqlEngine.MCP
{
    /// <summary>
    /// Interface that mimics the MCP (Model Context Protocol) tools that Claude uses
    /// These are the exact same operations I demonstrated when analyzing your database
    /// </summary>
    public interface IMcpToolsService
    {
        /// <summary>
        /// List all database schemas - equivalent to mssql-local:list_schemas
        /// </summary>
        Task<List<SchemaInfo>> ListSchemasAsync();

        /// <summary>
        /// List all user tables - equivalent to mssql-local:list_tables
        /// </summary>
        Task<List<TableReference>> ListTablesAsync();

        /// <summary>
        /// Search schema by keyword - equivalent to mssql-local:search_schema
        /// </summary>
        /// <param name="searchTerm">Term to search for in table/column names</param>
        Task<List<SchemaSearchResult>> SearchSchemaAsync(string searchTerm);

        /// <summary>
        /// Describe table columns - equivalent to mssql-local:describe_table
        /// </summary>
        /// <param name="tableName">Table name (Schema.Table or just Table)</param>
        Task<List<ColumnDescription>> DescribeTableAsync(string tableName);

        /// <summary>
        /// Get foreign key relationships - equivalent to mssql-local:foreign_keys
        /// </summary>
        /// <param name="tableName">Table name (Schema.Table or just Table)</param>
        Task<List<ForeignKeyRelation>> GetForeignKeysAsync(string tableName);

        /// <summary>
        /// Get table statistics - equivalent to mssql-local:table_stats
        /// </summary>
        /// <param name="tableName">Table name (Schema.Table or just Table)</param>
        Task<TableStatistics> GetTableStatsAsync(string tableName);

        /// <summary>
        /// Sample rows from table - equivalent to mssql-local:sample_rows
        /// </summary>
        /// <param name="tableName">Table name (Schema.Table or just Table)</param>
        /// <param name="rowCount">Number of rows to sample (default 5)</param>
        Task<List<Dictionary<string, object>>> SampleRowsAsync(string tableName, int rowCount = 5);

        /// <summary>
        /// Execute SELECT query - equivalent to mssql-local:query_select
        /// </summary>
        /// <param name="sqlQuery">SELECT statement to execute</param>
        Task<List<Dictionary<string, object>>> ExecuteSelectQueryAsync(string sqlQuery);

        /// <summary>
        /// Get query execution plan - equivalent to mssql-local:explain_query
        /// </summary>
        /// <param name="sqlQuery">SELECT statement to analyze</param>
        Task<string> ExplainQueryAsync(string sqlQuery);

        /// <summary>
        /// Get index information - equivalent to mssql-local:index_info
        /// </summary>
        /// <param name="tableName">Table name (Schema.Table or just Table)</param>
        Task<List<IndexInfo>> GetIndexInfoAsync(string tableName);

        /// <summary>
        /// List stored procedures - equivalent to mssql-local:list_procedures
        /// </summary>
        Task<List<ProcedureInfo>> ListProceduresAsync();

        /// <summary>
        /// List views - equivalent to mssql-local:list_views
        /// </summary>
        Task<List<ViewInfo>> ListViewsAsync();
    }

    // Data models that match the MCP tool responses
    public class SchemaInfo
    {
        public string SchemaName { get; set; }
    }

    public class TableReference
    {
        public string SchemaName { get; set; }
        public string TableName { get; set; }
    }

    public class SchemaSearchResult
    {
        public string SchemaName { get; set; }
        public string TableName { get; set; }
        public string ColumnName { get; set; }
        public string DataType { get; set; }
    }

    public class ColumnDescription
    {
        public string ColumnName { get; set; }
        public string DataType { get; set; }
        public bool IsNullable { get; set; }
        public int? CharacterMaximumLength { get; set; }
        public int? NumericPrecision { get; set; }
        public int? NumericScale { get; set; }
    }

    public class ForeignKeyRelation
    {
        public string ColumnName { get; set; }
        public string ReferencedTable { get; set; }
        public string ReferencedColumn { get; set; }
        public string ReferencedSchema { get; set; }
    }

    public class TableStatistics
    {
        public string SchemaName { get; set; }
        public string TableName { get; set; }
        public long RowCount { get; set; }
        public long TotalKB { get; set; }
        public long UsedKB { get; set; }
        public long DataKB { get; set; }
    }

    public class IndexInfo
    {
        public string IndexName { get; set; }
        public string IndexType { get; set; }
        public bool IsUnique { get; set; }
        public List<string> Columns { get; set; } = new();
    }

    public class ProcedureInfo
    {
        public string SchemaName { get; set; }
        public string ProcedureName { get; set; }
    }

    public class ViewInfo
    {
        public string SchemaName { get; set; }
        public string ViewName { get; set; }
    }
}