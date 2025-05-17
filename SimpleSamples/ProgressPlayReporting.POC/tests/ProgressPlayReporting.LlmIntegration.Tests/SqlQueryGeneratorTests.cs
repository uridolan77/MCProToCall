using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using ProgressPlayReporting.Core.Interfaces;
using Xunit;

namespace ProgressPlayReporting.LlmIntegration.Tests
{
    public class SqlQueryGeneratorTests
    {
        private readonly Mock<ILlmGateway> _llmGatewayMock;
        private readonly Mock<ILogger<SqlQueryGenerator>> _loggerMock;
        private readonly SqlQueryGenerator _queryGenerator;

        public SqlQueryGeneratorTests()
        {
            _llmGatewayMock = new Mock<ILlmGateway>();
            _loggerMock = new Mock<ILogger<SqlQueryGenerator>>();
            _queryGenerator = new SqlQueryGenerator(_llmGatewayMock.Object, _loggerMock.Object);
        }

        [Fact]
        public async Task GenerateSqlQueryAsync_ReturnsSqlQueryResult()
        {
            // Arrange
            var request = "Get all players who made deposits in the last 30 days";
            var schema = CreateSampleSchema();
            
            var jsonResponse = @"{
                ""query"": ""SELECT p.PlayerID, p.Name, p.Email FROM Players p INNER JOIN Deposits d ON p.PlayerID = d.PlayerID WHERE d.DepositDate >= DATEADD(day, -30, GETDATE())"",
                ""explanation"": ""This query retrieves all players who made deposits in the last 30 days by joining the Players table with the Deposits table and filtering based on the deposit date."",
                ""tablesUsed"": [""Players"", ""Deposits""],
                ""columnsReferenced"": [""Players.PlayerID"", ""Players.Name"", ""Players.Email"", ""Deposits.PlayerID"", ""Deposits.DepositDate""],
                ""warnings"": []
            }";
            
            _llmGatewayMock
                .Setup(g => g.GenerateCompletionAsync(It.IsAny<string>(), null))
                .ReturnsAsync(jsonResponse);

            // Act
            var result = await _queryGenerator.GenerateSqlQueryAsync(request, schema);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Query);
            Assert.NotNull(result.Explanation);
            Assert.Contains("SELECT", result.Query);
            Assert.NotEmpty(result.TablesUsed);
            Assert.NotEmpty(result.ColumnsReferenced);
        }

        [Fact]
        public async Task ValidateSqlQueryAsync_ReturnsValidationResult()
        {
            // Arrange
            var query = "SELECT * FROM Players WHERE JoinDate >= '2023-01-01'";
            var schema = CreateSampleSchema();
            
            var jsonResponse = @"{
                ""isValid"": true,
                ""correctedQuery"": ""SELECT * FROM Players WHERE JoinDate >= '2023-01-01'"",
                ""issues"": [],
                ""changeExplanation"": ""The query is valid and does not need any corrections.""
            }";
            
            _llmGatewayMock
                .Setup(g => g.GenerateCompletionAsync(It.IsAny<string>(), null))
                .ReturnsAsync(jsonResponse);

            // Act
            var result = await _queryGenerator.ValidateSqlQueryAsync(query, schema);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsValid);
            Assert.NotNull(result.CorrectedQuery);
            Assert.Empty(result.Issues);
            Assert.NotNull(result.ChangeExplanation);
        }

        private DatabaseSchema CreateSampleSchema()
        {
            return new DatabaseSchema
            {
                DatabaseName = "GamingDB",
                Tables = new List<TableSchema>
                {
                    new TableSchema
                    {
                        TableName = "Players",
                        Columns = new List<ColumnSchema>
                        {
                            new ColumnSchema { ColumnName = "PlayerID", DataType = "int", IsPrimaryKey = true, IsNullable = false },
                            new ColumnSchema { ColumnName = "Name", DataType = "nvarchar(100)", IsNullable = false },
                            new ColumnSchema { ColumnName = "Email", DataType = "nvarchar(255)", IsNullable = false },
                            new ColumnSchema { ColumnName = "JoinDate", DataType = "datetime", IsNullable = false }
                        }
                    },
                    new TableSchema
                    {
                        TableName = "Deposits",
                        Columns = new List<ColumnSchema>
                        {
                            new ColumnSchema { ColumnName = "DepositID", DataType = "int", IsPrimaryKey = true, IsNullable = false },
                            new ColumnSchema { ColumnName = "PlayerID", DataType = "int", IsNullable = false, IsForeignKey = true, ReferencedTable = "Players", ReferencedColumn = "PlayerID" },
                            new ColumnSchema { ColumnName = "Amount", DataType = "decimal(18,2)", IsNullable = false },
                            new ColumnSchema { ColumnName = "DepositDate", DataType = "datetime", IsNullable = false }
                        }
                    }
                }
            };
        }
    }
}
