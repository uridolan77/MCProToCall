# ProgressPlay Reporting MCP: Proof of Concept

This repository contains a proof of concept implementation of an AI-driven reporting system for ProgressPlay, built using the Model Context Protocol framework.

## Project Structure

The solution is organized into the following components:

```
ProgressPlayReporting.POC/
├── src/
│   ├── ProgressPlayReporting.Core/           # Core libraries and interfaces
│   ├── ProgressPlayReporting.SchemaExtractor/ # Schema extraction service
│   ├── ProgressPlayReporting.LlmIntegration/  # LLM integration
│   ├── ProgressPlayReporting.Validators/     # Validation components
│   └── ProgressPlayReporting.Api/            # API endpoints
├── tests/
│   ├── ProgressPlayReporting.SchemaExtractor.Tests/
│   ├── ProgressPlayReporting.LlmIntegration.Tests/
│   └── ProgressPlayReporting.Validators.Tests/
```

## Core Components

1. **Schema Extraction Service:** Extracts database schema information including tables, columns, relationships, and data types to provide context for the LLM.

2. **LLM Integration:** Provides a unified interface for interacting with large language models through the LLMGateway.

3. **SQL Query Generation:** Uses LLMs to translate natural language requests into SQL queries based on the database schema.

4. **Report Generation:** Analyzes data and creates comprehensive reports with insights, visualizations, and recommendations.

## API Endpoints

The application exposes the following API endpoints:

### Schema Controller

- `GET /api/schema` - Get the full database schema
- `GET /api/schema/tables` - Get all table names
- `GET /api/schema/tables/{tableName}` - Get schema for a specific table

### Query Controller

- `POST /api/query/generate` - Generate SQL from natural language
- `POST /api/query/validate` - Validate a SQL query
- `POST /api/query/explain` - Get a natural language explanation of a SQL query
- `POST /api/query/execute` - Execute a SQL query and return results

### Report Controller

- `POST /api/report/generate` - Generate a comprehensive report
- `POST /api/report/analyze` - Analyze data and extract insights
- `POST /api/report/visualize` - Generate visualization configurations

## Getting Started

### Prerequisites

- .NET 9.0 SDK
- SQL Server database
- Access to LLM services (Claude, GPT-4, etc.)

### Configuration

Edit the connection string in `src/ProgressPlayReporting.Api/appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "your_connection_string_here"
}
```

### Running the Application

```bash
cd ProgressPlayReporting.POC
dotnet build
cd src/ProgressPlayReporting.Api
dotnet run
```

The API will be available at `https://localhost:5001` or `http://localhost:5000`.

## Sample Usage

### Generate a SQL Query

```http
POST /api/query/generate
Content-Type: application/json

{
  "naturalLanguageRequest": "Show me the top 10 players by deposit amount in the last 30 days"
}
```

### Generate a Report

```http
POST /api/report/generate
Content-Type: application/json

{
  "naturalLanguageRequest": "Create a monthly deposit trend report for the last quarter",
  "sqlQuery": "SELECT MONTH(TransactionDate) as Month, SUM(Amount) as TotalDeposits FROM Deposits WHERE TransactionDate >= DATEADD(month, -3, GETDATE()) GROUP BY MONTH(TransactionDate) ORDER BY Month"
}
```

## License

This project is proprietary and confidential.
