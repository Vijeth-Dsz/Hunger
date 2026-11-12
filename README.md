# Hunger — food sharing for stronger communities

Hunger connects restaurants, caterers, wedding organizers, and grocery donors with verified nonprofits serving children and families. Donors can post surplus prepared food or groceries; recipient organizations can request donations, coordinate collection, and track delivery.

## Project structure

```text
backend/                 ASP.NET Core API, SQL Server schema, authentication
  Data/                  database initialization
  Models/                API and database models
  Security/              password hashing
frontend/                Angular application
  src/app/core/           shared account, donation models and API services
  src/app/features/       sign-in/registration and role-based dashboards
Hunger.sln                Visual Studio solution
Hunger.slnLaunch           Visual Studio multi-project startup profile
```

## Start the application

Requirements: .NET 10 SDK, Node.js/npm, Visual Studio 2026 with Node.js tools (for the frontend project in the solution), and SQL Server LocalDB.

Open `Hunger.sln`, select the **Hunger Full Stack** launch profile, and press **F5**. This starts the frontend and API on the local development URLs configured by their project launch settings. The API creates the configured SQL database on first startup.

Or use separate terminals from the repository root:

```powershell
dotnet run --project backend\Hunger.Api.csproj --launch-profile http
```

```powershell
Set-Location frontend
npm.cmd ci
npm.cmd start
```

In Development, Swagger UI is available at `<API URL>/swagger` and the OpenAPI document at `<API URL>/swagger/v1/swagger.json`. The frontend proxy uses the API launch URL; set `HUNGER_API_URL` to override it.

### Local configuration

The API reads its SQL connection string and administrator identity/password from .NET configuration. Keep them out of source control. For local development, initialize .NET User Secrets and set your own values:

```powershell
dotnet user-secrets set "ConnectionStrings:Default" "<your SQL Server connection string>" --project backend\Hunger.Api.csproj
dotnet user-secrets set "Admin:Name" "<administrator name>" --project backend\Hunger.Api.csproj
dotnet user-secrets set "Admin:Email" "<administrator email>" --project backend\Hunger.Api.csproj
dotnet user-secrets set "Admin:OrganizationName" "<organization name>" --project backend\Hunger.Api.csproj
dotnet user-secrets set "Admin:Password" "<strong administrator password>" --project backend\Hunger.Api.csproj
```

The administrator account is created or refreshed from these settings at API startup. For deployment, provide the same settings through a secret manager or environment variables such as `ConnectionStrings__Default` and `Admin__Password`.

## Implemented MVP

1. **Accounts and sign-in:** donors and recipient organizations can register; passwords are salted and PBKDF2-hashed; sign-in uses an HTTP-only session cookie.
2. **Organization verification:** new recipient organizations are pending review. Admins can view the verification queue and approve recipients.
3. **Donation listings:** donors publish prepared food or grocery donations with description, quantity, pickup window, storage instructions, and allergen notes.
4. **Request and match:** verified recipients browse open donations and request one; donors accept a request. A donation can only have one active claim.
5. **Pickup and delivery tracking:** the recipient confirms pickup; either party confirms delivery. Each status transition is recorded with its time and user.
6. **Role-based dashboards:** donors manage their listings, recipients find and track donations, and admins review organizations and monitor platform totals.
7. **Privacy and access:** donation endpoints require sign-in, recipient actions require approval, and exact pickup addresses are hidden from recipients until a request is accepted.

## API overview

- `POST /api/auth/register`, `POST /api/auth/login`, `GET /api/auth/me`, `POST /api/auth/logout`
- `GET /api/dashboard`, `GET /api/donations`, `GET /api/donations/{id}`, `POST /api/donations`
- `PUT /api/donations/{id}`, `DELETE /api/donations/{id}` (donors can edit or delete their available listings)
- `POST /api/donations/{id}/request`, `POST /api/donations/{id}/accept`
- `PATCH /api/donations/{id}/status`, `GET /api/donations/{id}/history`
- `GET /api/admin/recipients`, `POST /api/admin/recipients/{id}/approve`
- `GET /api/health`

The backend creates `Accounts`, `Donations`, `DonationRequests`, and `DonationStatusHistory` tables automatically. SQL values are parameterized, and API enums are serialized by name (for example, `"Donor"` and `"Available"`) to match the Angular models. Swagger UI and its OpenAPI document are enabled only in the Development environment.

## Final implementation plan

### Completed — pilot MVP

- Product branding and separate Angular/API projects.
- Donor and recipient registration, login/logout, password hashing, and protected routes.
- Admin seeding, recipient verification, and role-based permissions.
- Prepared-food and grocery donation creation, browsing, request/accept, cancellation, pickup/delivery status, and history.
- Summary cards and responsive donor, recipient, and admin dashboards.
- SQL LocalDB schema initialization and Visual Studio multi-startup configuration.

### Next — before a public pilot

1. Replace demo admin credentials with deployment secrets; implement account recovery, email verification, login throttling, and security monitoring.
2. Add verified organization document upload and an auditable verification/rejection workflow.
3. Agree and publish local food handling, eligibility, collection, allergen disclosure, expiry, and incident-reporting policies with qualified partners.
4. Add notifications for new requests, acceptance, pickup deadlines, cancellations, and delivery confirmation.
5. Add recipient capacity/preferences, donor search filters, and pickup coordination/contact details revealed only after acceptance.
6. Pilot with a small group of donors and nonprofits; test accessibility, mobile use, backups, audit retention, and support procedures.

### Later enhancements

- Volunteer/delivery partner accounts and route coordination.
- Email/SMS notifications, maps, impact reports, analytics, and multilingual support.
- Production hosting, managed SQL, secret management, observability, backups, and disaster recovery.

## Validation

```powershell
Set-Location frontend
npm.cmd test -- --watch=false
npm.cmd run build
Set-Location ..
dotnet build backend\Hunger.Api.csproj
```

**Important:** this is a functional development MVP, not yet a production-ready food safety or identity-verification system. Recipient verification currently uses administrator approval of registration details; coordinate food safety, privacy, and NGO vetting with local experts before launch.
