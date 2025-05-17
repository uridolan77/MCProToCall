using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using ProgressPlayReporting.Validators.Interfaces;
using ProgressPlayReporting.Validators.Sql;
using Xunit;

namespace ProgressPlayReporting.Validators.Tests
{
    public class SqlQueryValidatorTests
    {
        private readonly Mock<ILogger<SqlQueryValidator>> _loggerMock;
        private readonly SqlQueryValidator _validator;

        public SqlQueryValidatorTests()
        {
            _loggerMock = new Mock<ILogger<SqlQueryValidator>>();
            _validator = new SqlQueryValidator(_loggerMock.Object);
        }

        [Fact]
        public async Task ValidateQuery_ValidQuery_ReturnsValid()
        {
            // Arrange
            var query = "SELECT * FROM Users WHERE Age > 18";

            // Act
            var result = await _validator.ValidateQueryAsync(query);

            // Assert
            Assert.True(result.IsValid);
            Assert.Equal(ValidationSeverity.None, result.Severity);
            Assert.Empty(result.Issues);
        }

        [Fact]
        public async Task ValidateQuery_EmptyQuery_ReturnsInvalid()
        {
            // Arrange
            var query = "";

            // Act
            var result = await _validator.ValidateQueryAsync(query);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal(ValidationSeverity.Error, result.Severity);
            Assert.Single(result.Issues);
            Assert.Equal("SQL_EMPTY", result.Issues[0].Code);
        }

        [Fact]
        public async Task ValidateQuery_DangerousKeyword_ReturnsInvalid()
        {
            // Arrange
            var query = "SELECT * FROM Users; EXEC xp_cmdshell 'dir'";

            // Act
            var result = await _validator.ValidateQueryAsync(query);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal(ValidationSeverity.Critical, result.Severity);
            Assert.Contains(result.Issues, i => i.Code == "SQL_DANGEROUS_KEYWORD");
        }

        [Fact]
        public async Task ValidateQuery_MultipleSemicolons_ReturnsInvalid()
        {
            // Arrange
            var query = "SELECT * FROM Users;;";

            // Act
            var result = await _validator.ValidateQueryAsync(query);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal(ValidationSeverity.Error, result.Severity);
            Assert.Contains(result.Issues, i => i.Code == "SQL_CONSECUTIVE_SEMICOLONS");
        }

        [Fact]
        public async Task ValidateQuery_UnionSelect_AddsWarning()
        {
            // Arrange
            var query = "SELECT * FROM Users UNION SELECT * FROM Admins";

            // Act
            var result = await _validator.ValidateQueryAsync(query);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal(ValidationSeverity.Warning, result.Severity);
            Assert.Contains(result.Issues, i => i.Code == "SQL_UNION_SELECT");
        }

        [Fact]
        public async Task ValidateQuery_SqlComments_AddsInfo()
        {
            // Arrange
            var query = "SELECT * FROM Users -- Get all users";

            // Act
            var result = await _validator.ValidateQueryAsync(query);

            // Assert
            Assert.Contains(result.Issues, i => i.Code == "SQL_COMMENTS");
        }
    }
}
