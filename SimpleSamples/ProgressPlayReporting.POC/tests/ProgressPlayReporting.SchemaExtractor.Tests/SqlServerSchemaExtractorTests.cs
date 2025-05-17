using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using ProgressPlayReporting.Core.Interfaces;
using Xunit;

namespace ProgressPlayReporting.SchemaExtractor.Tests
{
    public class SqlServerSchemaExtractorTests
    {
        private readonly Mock<ILogger<SqlServerSchemaExtractor>> _loggerMock;
        private readonly SqlServerSchemaExtractor _extractor;

        public SqlServerSchemaExtractorTests()
        {
            _loggerMock = new Mock<ILogger<SqlServerSchemaExtractor>>();
            _extractor = new SqlServerSchemaExtractor(_loggerMock.Object);
        }

        [Fact(Skip = "Integration test requiring database connection")]
        public async Task ExtractSchemaAsync_ReturnsValidSchema()
        {
            // Arrange
            string connectionString = "your_test_connection_string";

            // Act
            var schema = await _extractor.ExtractSchemaAsync(connectionString);

            // Assert
            Assert.NotNull(schema);
            Assert.NotNull(schema.DatabaseName);
            Assert.NotEmpty(schema.Tables);
        }

        [Fact(Skip = "Integration test requiring database connection")]
        public async Task GetAllTableNamesAsync_ReturnsTableNames()
        {
            // Arrange
            string connectionString = "your_test_connection_string";

            // Act
            var tableNames = await _extractor.GetAllTableNamesAsync(connectionString);

            // Assert
            Assert.NotNull(tableNames);
            Assert.NotEmpty(tableNames);
        }

        [Fact(Skip = "Integration test requiring database connection")]
        public async Task GetTableSchemaAsync_ReturnsValidTableSchema()
        {
            // Arrange
            string connectionString = "your_test_connection_string";
            string tableName = "YourTestTable";

            // Act
            var tableSchema = await _extractor.GetTableSchemaAsync(connectionString, tableName);

            // Assert
            Assert.NotNull(tableSchema);
            Assert.Equal(tableName, tableSchema.TableName);
            Assert.NotEmpty(tableSchema.Columns);
        }
    }
}
