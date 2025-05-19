# SEP490 Backend

## Overview
SEP490 Backend is a comprehensive construction project management system API built with ASP.NET Core. The system provides a robust platform for managing construction projects, including site surveys, construction planning, resource allocation, progress tracking, and inspection reports.

## Project Goals
- Provide a reliable and efficient API for construction project management
- Enable real-time tracking of construction progress and resource allocation
- Facilitate communication between project stakeholders
- Support comprehensive logging and reporting capabilities
- Implement secure authentication and authorization mechanisms
- Optimize performance through effective caching strategies

## Technologies Used
- **Framework**: ASP.NET Core 8.0
- **Database**: PostgreSQL with Entity Framework Core 7.0
- **Authentication**: JWT Bearer token authentication
- **Caching**: Redis for distributed caching
- **Messaging**: SignalR for real-time notifications
- **Documentation**: Swagger/OpenAPI for API documentation
- **Logging**: Serilog for structured logging
- **Email**: MailKit for email notifications
- **Storage**: Google Drive API for document storage
- **Data Processing**: NPOI and ClosedXML for Excel operations
- **Security**: X.509 certificate for JWT signing

## Prerequisites

Before you begin, ensure you have the following installed:
- .NET 8.0 SDK or later
- PostgreSQL 13 or later
- Redis (for caching)
- Git

Follow these steps to set up the project locally:

1. Clone the repository
```bash
git https://github.com/lombeo/sep490-backend.git
cd sep490-backend
```

2. Create a `.env` file in the root directory with the following variables:
```
JWT_VALID_ISSUER=your_issuer
JWT_VALID_AUDIENCE=your_audience
JWT_SECRET=your_secret
MAIL_PASSWORD=your_mail_password
POSTGRES_CONNECTION_STRING=Host=localhost;Database=sep490;Username=postgres;Password=your_password
REDIS_CONNECTION_STRING=localhost:6379
```

3. Restore dependencies and build the project
```bash
dotnet restore
dotnet build
```

4. Run the database migrations
```bash
dotnet ef database update
```

5. Run the application
```bash
dotnet run
```

6. Access the Swagger documentation at `https://localhost:5001/swagger`

## Project Structure

### Core Components
- **Controllers**: RESTful API endpoints for various modules
- **Services**: Business logic implementation
- **Infra**: Infrastructure components including database context
- **DTO**: Data transfer objects
- **Migrations**: Database migration scripts

### Key Features
- **User Management**: Authentication, authorization, and user roles
- **Project Management**: Creating and tracking construction projects
- **Site Survey**: Managing site survey data and reports
- **Contract Management**: Handling contracts and their details
- **Construction Planning**: Creating and managing construction plans
- **Resource Management**: Allocation and mobilization of resources
- **Progress Tracking**: Monitoring construction progress
- **Inspection Reports**: Managing inspection data and findings
- **Notification System**: Email and real-time notifications
- **Action Logging**: Comprehensive activity logging

## Acknowledgments
- Thank you to the ASP.NET Core community for their excellent documentation and tools
- Special appreciation to the SEP490 Backend team for their dedication and hard work

## Contact
For issues or inquiries, please reach out to the team at `longddhe170376@fpt.edu.vn`
