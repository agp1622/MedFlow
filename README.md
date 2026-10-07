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
cp .env.example .env   # then fill in the values (git-ignored)
docker-compose up --build
```
- API: http://localhost:8080/swagger
- Client: http://localhost:5173

### Option B — Manual

**1. Start SQL Server (Docker)**
```bash
docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=$SA_PASSWORD" \
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
| `Payments:SecretKey` | Stripe secret key (`sk_...`); online payment is disabled while empty. Set via environment (`Payments__SecretKey`) |
| `Payments:WebhookSecret` | Stripe webhook signing secret (`whsec_...`). Point a Stripe webhook for `checkout.session.completed` at `POST /api/payments/webhook` |
| `Payments:Currency` | ISO currency for checkout (default `usd`) |
| `Reminders:LeadTimeHours` | How long before an appointment the reminder email is sent (1-168, default 24) |
| `Reminders:IntervalMinutes` | How often the reminder job runs (default 15) |
| `Reminders:MaxAttempts` | Send attempts per reminder before giving up (default 3) |
| `Waitlist:OfferHours` | How long a waitlist slot offer stays valid, never past the slot start (1-168, default 24) |
| `Waitlist:MaxOffersPerSlot` | Earliest-joined waiting patients emailed per freed slot (1-20, default 5) |
| `RateLimiting:WaitlistOfferPermitLimit` | Public waitlist-offer requests per client IP per 15 minutes (default 30) |

### Secrets

No real secret is committed. `appsettings*.json` hold empty values for these keys; supply them per environment:

| Key (env var) | Purpose |
|---------------|---------|
| `Jwt:Key` (`Jwt__Key`) | Token signing key, required everywhere; outside Development/Testing it must be 32+ chars and not a placeholder, otherwise the API **refuses to start** |
| `ConnectionStrings:DefaultConnection` (`ConnectionStrings__DefaultConnection`) | Database connection incl. password |
| `Email:AppPassword` (`Email__AppPassword`), `Email:SenderEmail` | SMTP credentials |
| `SeedUser:Email` / `SeedUser:Password` / `SeedUser:PatientEmail` / `SeedUser:PatientPassword` | Development demo accounts; seeding is skipped when unset |

- **Local:** `cd MedFlow.Api && dotnet user-secrets set "Jwt:Key" "<32+ chars>"` (same for the other keys), or copy `.env.example` to `.env` for docker-compose.
- **Azure:** App Service application settings with Key Vault references (`@Microsoft.KeyVault(SecretUri=...)`); `infra/main.bicep` takes secure parameters.
- Seeding and automatic migration run **only** when `ASPNETCORE_ENVIRONMENT=Development`.
- Never commit `appsettings.Production.json` or `.env`.

> **Leaked credentials:** earlier commits contained a real SMTP app password and seed account password. Treat them as compromised: rotate them at the provider, then scan and, if required, purge git history (e.g. `gitleaks detect` / `git filter-repo`, followed by a coordinated force-push) and invalidate old clones.

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
| POST | `/api/portal/invoices/{id}/checkout` | Patient starts online payment (returns Stripe Checkout URL) |
| POST | `/api/payments/webhook` | Stripe webhook (signature-verified, anonymous) |
| GET/POST | `/api/vitalsigns/patient/{id}` | Patient vitals |
| GET/POST | `/api/medicalnotes/patient/{id}` | Patient notes |

All endpoints require `Authorization: Bearer <token>` except `/api/auth/*`.

### Roles and the patient portal

Accounts are either **staff** of a clinic (Owner, Doctor, Nurse or Receptionist, see "Clinics and staff roles" below) or **Patient** portal users:

- **Staff**: all endpoints except `/api/auth/*` and `/api/portal/*`, limited by the clinic permission matrix.
- **Patient**: read-only access to their own records through `/api/portal/*` only. The patient is always taken from the token, never from a request parameter. A patient token never satisfies a staff permission.

| Method | Route | Role | Description |
|--------|-------|------|-------------|
| POST | `/api/patients/{id}/portal-invitation` | Owner, Doctor, Receptionist | Email a portal invitation (7-day, single-use link) |
| DELETE | `/api/patients/{id}/portal-access` | Owner, Doctor, Receptionist | Revoke portal access / cancel invitation |
| PUT | `/api/attachments/{id}/sharing` | Doctor | Share or unshare an attachment with the patient |
| PUT | `/api/medicalnotes/{id}/sharing` | Doctor | Share or unshare a note with the patient |
| POST | `/api/auth/accept-invitation` | Anonymous | Patient sets a password and signs in |
| GET | `/api/portal/me`, `appointments`, `prescriptions`, `invoices`, `attachments`, `notes` | Patient | Own data; attachments and notes only if shared |
| GET | `/api/portal/attachments/{id}/download` | Patient | Download a shared attachment |

Attachments and notes are **not shared by default**. Patients cannot use Google sign-in.
In Development, a fresh database is seeded with a demo patient (`patient.demo@medflow.local`,
password from `SeedUser:PatientPassword`, created only when `SeedUser:PatientEmail` and the password are configured) that has an appointment,
a prescription and an invoice. The seeder only runs when there are no users yet.

### Clinics and staff roles

Every record belongs to a **clinic**. A doctor who registers (email or Google) becomes the **Owner** of a new clinic; the
database migration `AddClinicsAndRoles` gave every existing doctor their own clinic (as Owner) and stamped every existing
record with it. A user belongs to one clinic. The role is read from the database on every request (the token's role claim is
for display only), so deactivating a member or changing a role applies immediately, and a user with no active membership
gets 403 everywhere. A record of another clinic answers exactly like a missing record (404).

Permissions live in one place, `MedFlow.Core.PermissionMatrix`, and are enforced by `[HasPermission(...)]` on every staff
action (a test fails if one is missing). Full matrix: `specs/045-clinic-roles/contracts/permission-matrix.md`.

| Area | Owner | Doctor | Nurse | Receptionist |
|------|:-----:|:------:|:-----:|:------------:|
| Patients: demographics and insurance (read / edit) | yes | yes | read | yes |
| Patient clinical fields (condition, allergies, notes, blood type) | yes | yes | read | no |
| Delete patient | yes | yes | no | no |
| Appointments and waitlist | yes | yes | read | yes |
| Intake links / portal invitations | yes | yes | no | yes |
| Intake review, availability | yes | yes | no | no |
| Prescriptions | yes | yes | read | no |
| Invoices and payments | yes | yes | no | yes |
| Vitals, notes (create) | yes | yes | yes | no |
| Notes: share / delete; attachments, labs, allergies/problems/medications (write) | yes | yes | read only | no |
| Audit log of a patient | any patient | own patients | no | no |
| Reports | clinic-wide | own data | no | no |
| Staff, invitations, roles, clinic name | yes | no | no | no |

Staff invitation: an Owner invites by email (`POST /api/staff/invitations`, roles Doctor, Nurse or Receptionist); the emailed
link carries a random single-use token (stored only as a SHA-256 hash, 7 days, rate limited acceptance at
`POST /api/auth/accept-staff-invitation`). Every failure answers the same generic error, and inviting an address that
already has an account looks exactly like success (nothing is sent), so account existence is not revealed. The last active
Owner can never be demoted or deactivated.

Verification status: the migration's SQL was reviewed and structurally tested (it cannot run on the in-memory test provider); run it against a copy of a real database before deploying (see `specs/045-clinic-roles/quickstart.md`). The client was verified by build and lint only.

Behaviour notes: lists, dashboard and the schedule are clinic-wide; a self-registered doctor now shows role `Owner`; Receptionists
get patient primary condition, allergies, notes and blood type withheld (and preserved when they edit); staff-created patients,
appointments and invoices are linked to a clinic doctor (optional `doctorId`, default the caller if a clinician, else the clinic
Owner); notes can be authored by nurses (no FK to `Doctors`).

### Appointment reminders

A background job emails patients a reminder (email only) before Pending/Confirmed appointments. The email links to
`/appointment-response?token=...`, where the patient can confirm or cancel without signing in. Every attempt is logged on
the appointment. The job runs inside the API process, so run a single API instance.

| Method | Route | Role | Description |
|--------|-------|------|-------------|
| POST | `/api/appointment-response/lookup` | Anonymous (token) | Minimal appointment details for the link |
| POST | `/api/appointment-response/respond` | Anonymous (token) | `{ token, action: "Confirm" \| "Cancel" }` |
| GET | `/api/appointments/{id}/reminders` | Doctor | Delivery log and patient response (own appointments only) |

### Insurance and claim export draft

Patients store insurance provider, policy number, group number, payer ID, and the subscriber's name, date of birth and
relationship (Self, Spouse, Child, Other). From an invoice, a doctor can export a **draft** claim worksheet.

| Method | Route | Role | Description |
|--------|-------|------|-------------|
| GET | `/api/invoices/{id}/claim-export?format=json\|csv` | Doctor | Draft CMS-1500 (02/12) item data for the doctor's own invoice; 404 otherwise; audited as a view of the invoice |

What it is and is not: the export is a data worksheet that uses CMS-1500 item numbers and lists missing items. It is
**not** the official CMS-1500 form, **not** an X12 837 file (no 837 generation or validation exists), and **not** validated
by any payer or clearinghouse. MedFlow does not store CPT/HCPCS, ICD-10, provider NPI or federal tax ID, so those items are
always reported as missing. The item numbering is a best-effort mapping.

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
               jwtKey=<32_CHAR_SECRET> \
               emailAppPassword=<SMTP_APP_PASSWORD>
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
