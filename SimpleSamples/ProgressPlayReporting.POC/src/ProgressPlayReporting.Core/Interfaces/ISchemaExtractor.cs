using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ProgressPlayReporting.Core.Interfaces
{
    public interface ISchemaExtractor
    {
        Task<DatabaseSchema> ExtractSchemaAsync(string connectionString);
        Task<TableSchema> GetTableSchemaAsync(string connectionString, string tableName);
        Task<IEnumerable<string>> GetAllTableNamesAsync(string connectionString);
    }

    public class DatabaseSchema
    {
        public string DatabaseName { get; set; }
        public List<TableSchema> Tables { get; set; } = new List<TableSchema>();
        public List<string> Dimensions { get; set; } = new List<string>();
        public List<string> Facts { get; set; } = new List<string>();
    }

    public class TableSchema
    {
        public string TableName { get; set; }
        public List<ColumnSchema> Columns { get; set; } = new List<ColumnSchema>();
        public List<IndexSchema> Indexes { get; set; } = new List<IndexSchema>();
        public List<ForeignKeySchema> ForeignKeys { get; set; } = new List<ForeignKeySchema>();
    }

    public class ColumnSchema
    {
        public string ColumnName { get; set; }
        public string DataType { get; set; }
        public bool IsNullable { get; set; }
        public bool IsPrimaryKey { get; set; }
        public bool IsForeignKey { get; set; }
        public string ReferencedTable { get; set; }
        public string ReferencedColumn { get; set; }
    }

    public class IndexSchema
    {
        public string IndexName { get; set; }
        public bool IsUnique { get; set; }
        public List<string> Columns { get; set; } = new List<string>();
    }

    public class ForeignKeySchema
    {
        public string ConstraintName { get; set; }
        public string ColumnName { get; set; }
        public string ReferencedTable { get; set; }
        public string ReferencedColumn { get; set; }
    }
}
