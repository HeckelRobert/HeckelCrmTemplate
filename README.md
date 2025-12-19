# Heckel CRM Light

A lightweight CRM system built with ASP.NET Core 10 for managing Leads and Offers with optional Lexoffice integration.

## Features

- **Lead Management**: Collect and manage leads
- **Offer Management**: Create and track quotes/offers (with optional Lexoffice integration)
- **Request Management**: Handle quote requests from contacts
- **Partner Management**: Assign leads to sales partners via partner IDs
- **User-Friendly Web UI**: Modern, responsive web interface for viewing and managing leads and offers
- **Entra ID Authentication**: Secure login using Microsoft Entra ID
- **Optional Lexoffice Integration**: Automatic contact creation and quote management (optional extension)
- **Clean Architecture**: Domain-driven design with separation of concerns

## Technology Stack

- **.NET 10.0** (ASP.NET Core)
- **Entity Framework Core** (SQL Server)
- **Microsoft Entra ID** (Authentication)
- **Lexoffice API** (Optional accounting integration)
- **OpenAPI** (API documentation)

## Project Structure

```
HeckelCrm/
├── src/
│   ├── HeckelCrm.Core/             # Domain entities, interfaces, business logic and DTOs
│   ├── HeckelCrm.Infrastructure/   # Data access and external services
│   ├── HeckelCrm.Api/              # Web API controllers and configuration
│   ├── HeckelCrm.Web/              # Web UI (MVC)
│   └── HeckelCrm.Tests/            # Unit and integration tests
└── README.md
```

## Prerequisites

- .NET 10.0 SDK
- SQL Server (LocalDB, SQL Server Express, or Azure SQL)
- Microsoft Entra ID tenant
- Lexoffice account with API access (optional, for accounting integration)

## Setup Instructions

### 1. Clone the Repository

```bash
git clone <repository-url>
cd heckel_crm_light
```

### 2. Configure Database Connection

Update `appsettings.json` with your database connection string:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=your-server;Database=HeckelCrm;User Id=your-user;Password=your-password;"
  }
}
```

### 3. Configure Entra ID (formerly Azure AD)

1. Register an application in Azure Portal
2. **Configure Redirect URIs** (IMPORTANT):
   - Go to Azure Portal → App registrations → Your app → Authentication
   - Add the following Redirect URIs:
     - For local development: `https://localhost:54806/signin-oidc` (or your configured port)
     - For production: `https://your-domain.com/signin-oidc`
   - Make sure the URI matches exactly (including protocol, port, and path)
3. **Configure Groups Claims** (IMPORTANT):
   - Go to Azure Portal → App registrations → Your app → Token configuration
   - Click "Add groups claim"
   - Select "Security groups" or "All groups" (depending on your needs)
   - Select "Group ID" as the claim type
   - Save the configuration
   - **Note**: Users must be added as **Members** of the groups, not just as Owners. Only Members receive group claims in tokens.
4. **Create Security Groups**:
   - Go to Azure Portal → Microsoft Entra ID → Groups
   - Create two security groups: "Admins" and "Partners"
   - Add users as **Members** (not just Owners) to the respective groups
   - Copy the **Object ID** of each group (you'll need this for configuration)
5. Configure API permissions
   - **Certificates & secrets**: Create a new client secret (used by the backend for token validation and On‑Behalf‑Of flow)
   - **Expose an API**:
     - Add an Application ID URI (e.g. `api://your-client-id`)
     - Add a scope with the name `api.access` (this is what the Web UI requests when calling the API)
   - **API permissions** (for this app registration):
     - Under your own API (e.g. `Heckel_CRM_DEMO`): add the **Delegated** permission `api.access` and grant admin consent
     - Under **Microsoft Graph**: at minimum add `openid`, `profile`, `email`, and `offline_access`; `User.Read` is optional and currently not required by the app
6. Update `appsettings.json`:

```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "Domain": "yourdomain.onmicrosoft.com",
    "TenantId": "your-tenant-id",
    "ClientId": "your-client-id",
    "CallbackPath": "/signin-oidc",
    "PartnerGroupId": "your-partner-group-id",
    "AdminGroupId": "your-admin-group-id"
  },
  "AzureAd": {
    "Audience": "api://your-client-id"
  },
  "Api": {
    "Audience": "api://your-client-id"
  }
}
```

**Important**: 
- The `CallbackPath` in `appsettings.json` must match the Redirect URI configured in Entra ID. The default is `/signin-oidc`.
- `AzureAd:Audience` is **required** - this is the audience that the API accepts when validating JWT tokens.
- `Api:Audience` is **optional** - if not set, it automatically uses `AzureAd:Audience`. Both must have the same value (e.g. `api://your-client-id`).

### 4. Configure Lexoffice API (Optional)

Lexoffice integration is optional. The application works completely without Lexoffice - you can create and manage Leads and Offers without it.

**To enable Lexoffice integration:**
1. After deployment, log in as Admin
2. Navigate to "Admin-Einstellungen" (Admin Settings)
3. Enter your Lexoffice API Key in the "Lexoffice API Key" field
4. Save the settings

**Note**: The Lexoffice API Key is configured via the Admin Settings UI, not in `appsettings.json`. If Lexoffice is not configured, the CRM will work without it. Lexoffice features will be disabled, but all other functionality remains available.

### 5. Local Development Setup

For local development, create a local configuration file that won't be committed to Git:

1. Copy `appsettings.Example.json` to `appsettings.Local.json`:
   ```bash
   cp src/HeckelCrm.Api/appsettings.Example.json src/HeckelCrm.Api/appsettings.Local.json
   cp src/HeckelCrm.Web/appsettings.Example.json src/HeckelCrm.Web/appsettings.Local.json
   ```

2. Fill in your actual configuration values in `appsettings.Local.json`

**Note**: `appsettings.Local.json` is gitignored and will not be committed to the repository.

#### Alternative: Using .NET User Secrets

You can also use .NET User Secrets for local development:

```bash
cd src/HeckelCrm.Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\\mssqllocaldb;Database=HeckelCrm;Trusted_Connection=true"
dotnet user-secrets set "AzureAd:TenantId" "your-tenant-id"
dotnet user-secrets set "AzureAd:ClientId" "your-client-id"
dotnet user-secrets set "AzureAd:ClientSecret" "your-client-secret"
```

User Secrets are automatically gitignored and stored securely on your local machine.

### 6. Run Database Migrations

```bash
dotnet ef database update --project src/HeckelCrm.Infrastructure --startup-project src/HeckelCrm.Api
```

Or use the automatic database creation (for development):

The application will automatically create the database on first run using `EnsureCreated()`.

### 7. Run the Application

**Run the API:**
```bash
dotnet run --project src/HeckelCrm.Api
```

**Run the Web UI (requires API to be running):**
```bash
dotnet run --project src/HeckelCrm.Web
```

**Or run from project directories:**
```bash
# API
cd src/HeckelCrm.Api
dotnet run

# Web (in another terminal)
cd src/HeckelCrm.Web
dotnet run
```

The application will be available at:
- **Web UI**: `https://localhost:54806` (or your configured port)
- **API**: `https://localhost:5001/api`
- **OpenAPI Documentation**: `https://localhost:5001/openapi/v1.json`

### 8. Testing Locally

1. **Management UI**: Visit `/` and authenticate with Entra ID to access the dashboard
2. **API**: Use OpenAPI at `/openapi/v1.json` to view API documentation

## Hosting the Application

The application can be hosted on any server that supports .NET 10.0 and SQL Server. The following sections provide general guidance for hosting.

### General Hosting Requirements

- **.NET 10.0 Runtime** (for production) or SDK (for development)
- **SQL Server** (SQL Server Express, Standard, or Azure SQL)
- **HTTPS** (required for production, especially with Entra ID authentication)
- **Reverse Proxy** (optional, but recommended - e.g., Nginx, IIS, or Traefik)

### Docker Deployment

The project includes Dockerfiles and docker-compose configuration for containerized deployment:

1. **Build and run with Docker Compose:**
   ```bash
   docker-compose up -d
   ```

2. **Configure environment variables** (optional, for local development):
   Create a `.env` file in the project root with:
   ```bash
   DB_PASSWORD=your-strong-password
   DB_NAME=HeckelCrm
   AZURE_AD_CLIENT_ID=your-client-id
   AZURE_AD_TENANT_ID=your-tenant-id
   AZURE_AD_CLIENT_SECRET=your-client-secret
   AZURE_AD_INSTANCE=https://login.microsoftonline.com/
   AZURE_AD_PARTNER_GROUP_ID=your-partner-group-object-id
   AZURE_AD_ADMIN_GROUP_ID=your-admin-group-object-id
   AZURE_AD_AUDIENCE=api://your-client-id
   # API_AUDIENCE is optional - if not set, it automatically uses AZURE_AD_AUDIENCE
   # API_AUDIENCE=api://your-client-id
   LEXOFFICE_BASE_URL=https://api.lexware.io/v1
   ```

   **Note:** 
   - `AZURE_AD_AUDIENCE` is **required** - this is the audience that the API accepts when validating JWT tokens.
   - `API_AUDIENCE` is **optional** - if not set, it automatically uses `AZURE_AD_AUDIENCE`. Both must have the same value (e.g. `api://your-client-id`).
   - For production deployment, use GitHub Secrets (see CI/CD section below). External Links and Lexoffice API Key are configured via the Admin Settings UI after deployment.

3. **Database migrations:**
   Migrations are automatically applied when the API container starts. No manual migration step is required.

### CI/CD with GitHub Actions

The project includes a GitHub Actions workflow (`.github/workflows/deploy.yml`) for automated deployment. To use it:

1. **Configure GitHub Secrets:**
   - `DEPLOY_HOST`: Your server IP address
   - `DEPLOY_USER`: SSH username (e.g., `root`)
   - `DEPLOY_SSH_KEY`: Private SSH key for server access
   - `DEPLOY_SSH_PASSPHRASE`: SSH key passphrase (if applicable)
   - `DEPLOY_PORT`: SSH port (default: 22)
   - `DEPLOYMENT_DIRECTORY`: Deployment directory path (default: `/opt/heckel-crm`)
   - `DB_PASSWORD`: Database password
   - `DB_NAME`: Database name (default: `HeckelCrm`)
   - `AZURE_AD_CLIENT_ID`: Entra-ID client ID
   - `AZURE_AD_TENANT_ID`: Entra-ID tenant ID
   - `AZURE_AD_CLIENT_SECRET`: Entra-ID client secret
   - `AZURE_AD_INSTANCE`: Entra-ID instance URL (default: `https://login.microsoftonline.com/`)
   - `AZURE_AD_PARTNER_GROUP_ID`: Object ID of the Entra ID security group for partners
   - `AZURE_AD_ADMIN_GROUP_ID`: Object ID of the Entra ID security group for admins
   - `AZURE_AD_AUDIENCE`: API audience (e.g. `api://your-client-id`) - **Required**. This is the audience that the API accepts when validating JWT tokens.
   - `API_AUDIENCE`: (Optional) Same audience value, used by the Web UI to request access tokens for the API. If not set, it automatically uses `AZURE_AD_AUDIENCE`. Both must have the same value (e.g. `api://your-client-id`).
   - `LEXOFFICE_BASE_URL`: Lexoffice API base URL (optional)


2. **Prepare your server:**
   - Install Docker and Docker Compose
   - Create deployment directory: `mkdir -p /opt/heckel-crm` (or your configured path)

3. **Deploy:**
   - Push to `master` branch triggers automatic deployment
   - Or manually trigger via GitHub Actions → "Deploy to Debian Server" → Run workflow

### Example: Hosting on Debian with nginx

If you choose to host on a Debian-based server with nginx, here are the specific steps:

1. **Create a Debian/Ubuntu Server:**
   - Choose Ubuntu 22.04 or Debian 12
   - Minimum: 2 vCPU, 4 GB RAM
   - Recommended: 3 vCPU, 8 GB RAM

2. **Install Docker:**
   ```bash
   curl -fsSL https://get.docker.com | sh
   apt install docker-compose
   apt install docker-compose-plugin -y
   ```

3. **Set up deployment directory:**
   ```bash
   mkdir -p /opt/heckel-crm
   cd /opt/heckel-crm
   ```

4. **Configure GitHub Actions:**
   - Add GitHub Secrets (see CI/CD section above)
   - Push to `master` branch or trigger workflow manually

5. **First deployment:**
   - The GitHub Actions workflow will build Docker images and deploy automatically
   - Database migrations will run automatically
   - Check logs: `docker-compose logs -f`

6. **SSL/TLS with nginx:**
   ```bash
   apt install nginx certbot python3-certbot-nginx -y
   certbot --nginx -d your-domain.de
   ```

### Database Backups

Set up automated backups for production:

```bash
# Add to crontab (crontab -e)
0 2 * * * docker exec heckel-crm-db /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "YourPassword" -Q "BACKUP DATABASE HeckelCrm TO DISK='/var/opt/mssql/backup/backup-$(date +\%Y\%m\%d).bak'"
```

## Web UI

The application includes a modern, responsive web interface accessible at the root URL (`/`). The UI provides:

- **Dashboard**: Overview with statistics and quick access to leads and offers
- **Leads Management**: View all leads with filtering options, detailed lead information
- **Offers Management**: View all offers with status tracking, Lexoffice integration links, and acceptance time tracking
- **Request Management**: Handle quote requests from contacts
- **Responsive Design**: Works seamlessly on desktop, tablet, and mobile devices
- **Modern UI**: Built with Bootstrap 5 and Bootstrap Icons for a professional look

### Accessing the UI

1. Navigate to `https://localhost:5001` (or your configured URL)
2. Authenticate using Entra ID
3. Use the navigation menu to access different sections


## Security Considerations

1. **HTTPS**: Always use HTTPS in production
2. **API Authentication**: All management endpoints require Entra ID authentication
3. **SQL Injection**: Entity Framework Core prevents SQL injection
4. **CORS**: Configure CORS if needed for external integrations
5. **Secrets Management**: Use environment variables or secure key management for secrets

## Development

### Running Tests

```bash
dotnet test
```

### Code Style

This project follows Microsoft's C# coding conventions and clean architecture principles.

## License

This project is designed to be reusable and can be pushed to a public Git repository for clients to build their own CRM systems.

## Support

For issues and questions, please create an issue in the repository.
