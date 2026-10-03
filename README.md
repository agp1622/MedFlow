# MedFlow — Doctor Patient Management SaaS

A production-grade patient management platform for doctors.  
**Stack:** ASP.NET Core 8 · EF Core · SQL Server · ASP.NET Identity · JWT · React 18 · TypeScript · Vite · TanStack Query · Zustand · Tailwind CSS · Azure

---

## Project Structure

```
MedFlow/
├── MedFlow.Core/               # Domain layer: entities, DTOs, interfaces, enums
├── MedFlow.Infrastructure/     # Data layer: EF Core, repositories, Identity
├── MedFlow.Api/                # Web API: controllers, middleware, JWT auth
├── medflow-client/             # React + TypeScript frontend
├── infra/                      # Azure Bicep infrastructure-as-code
├── Dockerfile                  # Multi-stage Docker build
├── docker-compose.yml          # Local dev with SQL Server container
└── azure-pipelines.yml         # CI/CD: Build → Staging → Production
```

---

## Quick Start (Local)

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org)
- [Docker Desktop](https://www.docker.com/products/docker-desktop) (for local SQL Server)

### Option A — Docker Compose (recommended)
```bash
docker-compose up --build
```
- API: http://localhost:8080/swagger
- Client: http://localhost:5173

### Option B — Manual

**1. Start SQL Server (Docker)**
```bash
docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=MedFlow_Dev_2024!" \
  -p 1433:1433 mcr.microsoft.com/mssql/server:2022-latest
```

**2. Run the API**
```bash
cd MedFlow.Api
dotnet run
# API at https://localhost:7001 | Swagger at https://localhost:7001/swagger
```
EF Core will auto-migrate on startup in Development mode.

**3. Run the React client**
```bash
cd medflow-client
npm install
npm run dev
# Client at http://localhost:5173
```

---

## Configuration

### API (`MedFlow.Api/appsettings.json`)
| Key | Description |
|-----|-------------|
| `ConnectionStrings:DefaultConnection` | SQL Server connection string |
| `Jwt:Key` | **Secret key — must be 32+ chars in production** |
| `Jwt:Issuer` | Token issuer (default: `MedFlowApi`) |
| `Jwt:Audience` | Token audience (default: `MedFlowClient`) |
| `Jwt:ExpiryMinutes` | Token lifetime in minutes |
| `AllowedOrigins` | CORS allowed origins array (the first entry is used for links in emails) |
| `Reminders:LeadTimeHours` | How long before an appointment the reminder email is sent (1-168, default 24) |
| `Reminders:IntervalMinutes` | How often the reminder job runs (default 15) |
| `Reminders:MaxAttempts` | Send attempts per reminder before giving up (default 3) |

Use environment variables or Azure Key Vault for production secrets.  
Never commit `appsettings.Production.json` to source control.

### Client (`medflow-client/.env.local`)
```env
VITE_API_URL=https://localhost:7001
```

---

## Database Migrations

```bash
# From solution root
dotnet ef migrations add InitialCreate \
  --project MedFlow.Infrastructure \
  --startup-project MedFlow.Api

dotnet ef database update \
  --project MedFlow.Infrastructure \
  --startup-project MedFlow.Api
```

---

## API Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/auth/register` | Register a new doctor |
| POST | `/api/auth/login` | Login, returns JWT |
| GET | `/api/dashboard` | Dashboard stats |
| GET/POST | `/api/patients` | List / create patients |
| GET/PUT/DELETE | `/api/patients/{id}` | Patient CRUD |
| GET/POST | `/api/appointments` | List / schedule appointments |
| PATCH | `/api/appointments/{id}/status` | Update status |
| GET/POST | `/api/prescriptions` | List / create prescriptions |
| GET/POST | `/api/invoices` | List / create invoices |
| PATCH | `/api/invoices/{id}/mark-paid` | Mark invoice paid |
| GET/POST | `/api/vitalsigns/patient/{id}` | Patient vitals |
| GET/POST | `/api/medicalnotes/patient/{id}` | Patient notes |

All endpoints require `Authorization: Bearer <token>` except `/api/auth/*`.

### Roles and the patient portal

Accounts have one of two roles, carried as a role claim in the JWT:

- **Doctor**: everything above. All endpoints except `/api/auth/*` and `/api/portal/*` require this role.
- **Patient**: read-only access to their own records through `/api/portal/*` only. The patient is always taken from the token, never from a request parameter.

| Method | Route | Role | Description |
|--------|-------|------|-------------|
| POST | `/api/patients/{id}/portal-invitation` | Doctor | Email a portal invitation (7-day, single-use link) |
| DELETE | `/api/patients/{id}/portal-access` | Doctor | Revoke portal access / cancel invitation |
| PUT | `/api/attachments/{id}/sharing` | Doctor | Share or unshare an attachment with the patient |
| PUT | `/api/medicalnotes/{id}/sharing` | Doctor | Share or unshare a note with the patient |
| POST | `/api/auth/accept-invitation` | Anonymous | Patient sets a password and signs in |
| GET | `/api/portal/me`, `appointments`, `prescriptions`, `invoices`, `attachments`, `notes` | Patient | Own data; attachments and notes only if shared |
| GET | `/api/portal/attachments/{id}/download` | Patient | Download a shared attachment |

Attachments and notes are **not shared by default**. Patients cannot use Google sign-in.
In Development, a fresh database is seeded with a demo patient (`patient.demo@medflow.local`,
password from `SeedUser:PatientPassword`, default `MedFlowPatient2026!`) that has an appointment,
a prescription and an invoice. The seeder only runs when there are no users yet.

### Appointment reminders

A background job emails patients a reminder (email only) before Pending/Confirmed appointments. The email links to
`/appointment-response?token=...`, where the patient can confirm or cancel without signing in. Every attempt is logged on
the appointment. The job runs inside the API process, so run a single API instance.

| Method | Route | Role | Description |
|--------|-------|------|-------------|
| POST | `/api/appointment-response/lookup` | Anonymous (token) | Minimal appointment details for the link |
| POST | `/api/appointment-response/respond` | Anonymous (token) | `{ token, action: "Confirm" \| "Cancel" }` |
| GET | `/api/appointments/{id}/reminders` | Doctor | Delivery log and patient response (own appointments only) |

Access-control tests: `dotnet test MedFlow.Api.Tests`.

---

## Azure Deployment

### 1. Provision Infrastructure
```bash
az group create --name medflow-rg --location eastus

az deployment group create \
  --resource-group medflow-rg \
  --template-file infra/main.bicep \
  --parameters environmentName=prod \
               sqlAdminPassword=<SECURE_PASSWORD> \
               jwtKey=<32_CHAR_SECRET>
```

### 2. CI/CD (Azure DevOps)
1. Import `azure-pipelines.yml` into Azure DevOps
2. Set service connection name in pipeline variables (`azureSubscription`)
3. Add pipeline secret variable: `AZURE_STATIC_WEB_APPS_API_TOKEN`
4. Push to `develop` → deploys to staging slot
5. Push to `main` → deploys to production

### Architecture
```
GitHub/Azure DevOps
        │
        ▼
Azure DevOps Pipeline
   ├── Build API (.NET publish)
   ├── Build Client (npm run build)
   ├── Deploy API → Azure App Service (B2)
   └── Deploy Client → Azure Static Web Apps
                │
                ▼
        Azure SQL Server (S1)
```

---

## Security Checklist
- [ ] Replace `Jwt:Key` with a cryptographically secure 64-char key in production
- [ ] Store secrets in Azure Key Vault (not appsettings)
- [ ] Enable SQL Server firewall rules (restrict to App Service IPs)
- [ ] Enable HTTPS-only on App Service
- [ ] Set `ASPNETCORE_ENVIRONMENT=Production`
- [ ] Review CORS `AllowedOrigins` to match your exact frontend URL
- [ ] Enable Azure App Service managed identity for Key Vault access
- [ ] Set up Application Insights for monitoring

---

## Features

| Module | Features |
|--------|----------|
| **Auth** | Register, login, JWT with expiry, lockout protection |
| **Patients** | Full CRUD, search, pagination, soft delete, blood type, insurance |
| **Appointments** | Schedule, confirm, cancel, status management |
| **Prescriptions** | Create, track expiry, auto-status updates |
| **Billing** | Invoice generation, mark paid, overdue tracking |
| **Vitals** | Record and track patient vital signs with BMI calc |
| **Notes** | Clinical notes per patient with visit type |
| **Dashboard** | Live stats, today's schedule, recent patients |
| **Patient portal** | Invite patients, role-based access, read-only view of appointments, prescriptions, invoices, plus documents and notes the doctor chooses to share |
