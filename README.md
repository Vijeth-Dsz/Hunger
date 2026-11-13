# Hunger — food sharing for stronger communities

Hunger connects restaurants, caterers, wedding organizers, and grocery donors with verified nonprofits serving children and families. Donors can post surplus prepared food or groceries; recipient organizations can request donations, coordinate collection, and track delivery.

## Project structure

```text
backend/                 ASP.NET Core API, EF Core data access, SQL Server schema
  Controllers/           auth, role, donation, need, and admin endpoints
  Data/                  entities, DbContext, compiled queries, initialization
  Models/                API contracts and entity mappings
frontend/                Angular application
  src/app/core/           shared account, donation models and API services
  src/app/features/       sign-in/registration and role-based dashboards
Hunger.sln                Visual Studio solution
Hunger.slnLaunch           Visual Studio multi-project startup profile
```

## Start the application

Requirements: .NET 10 SDK, Node.js/npm, Visual Studio 2026 with Node.js tools (for the frontend project in the solution), and SQL Server LocalDB.

Open `Hunger.sln`, select the **Hunger Full Stack** launch profile, and press **F5**. This starts the ASP.NET Core API at `http://localhost:3000` and the Angular frontend. The API creates the configured SQL database and Hunger tables on first startup.

Or use separate terminals from the repository root:

```powershell
dotnet run --project backend\Hunger.Api.csproj --launch-profile http
```

```powershell
Set-Location frontend
npm.cmd ci
npm.cmd start
```

In Development, Swagger UI is available at `http://localhost:3000/swagger` and the OpenAPI document at `http://localhost:3000/swagger/v1/swagger.json`. To launch the API with Swagger opened automatically, select the backend `swagger` launch profile in Visual Studio or run `dotnet run --project backend\Hunger.Api.csproj --launch-profile swagger`. The frontend proxy uses the API launch URL; set `HUNGER_API_URL` to override it.

### Local configuration

The API reads its SQL connection string and administrator identity/password from .NET User Secrets. The configured database is `db` on `(localdb)\MSSQLLocalDB`; new Hunger accounts and donation records are written there. Keep administrator credentials out of source control. Configure the local connection string and admin identity/password with:

```powershell
dotnet user-secrets set "ConnectionStrings:Default" "Server=(localdb)\MSSQLLocalDB;Database=db;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;" --project backend\Hunger.Api.csproj
dotnet user-secrets set "Admin:Name" "<administrator name>" --project backend\Hunger.Api.csproj
dotnet user-secrets set "Admin:Email" "<administrator email>" --project backend\Hunger.Api.csproj
dotnet user-secrets set "Admin:OrganizationName" "<organization name>" --project backend\Hunger.Api.csproj
dotnet user-secrets set "Admin:Password" "<strong administrator password>" --project backend\Hunger.Api.csproj
```

The administrator account is created or refreshed from these settings at API startup. The admin sign-in page is `http://localhost:4200/admin/login`; sign in with the configured `Admin:Email` and `Admin:Password`. There is no shared/default admin password: set those values for the intended administrator and share them securely rather than committing or displaying a password. For deployment, provide the same settings through a secret manager or environment variables such as `ConnectionStrings__Default` and `Admin__Password`.

## Implemented MVP

1. **Accounts and sign-in:** registration roles are read from the database; passwords are salted and PBKDF2-hashed; sign-in uses an HTTP-only session cookie.
2. **Organization verification:** donor and recipient registration requires a PDF, JPEG, or PNG proof document up to 5 MB. Admins can view/download it before approval; accounts stay held from publishing, requesting, or contributing until approved.
3. **Food and goods listings:** donors publish prepared food, groceries, or stationery with quantity, pickup window, a use-by date for food and groceries, storage instructions, and allergen notes. Available listings support edit and delete.
4. **Donation requests and fulfillment:** verified recipients request available listings; donors accept a request; recipients record pickup and successful receipt. Status history is stored for tracking.
5. **Community needs and contributions:** recipients can create, edit, and delete requests that have no contributions; verified donors can contribute partial quantities with a use-by date for food and groceries; recipients confirm receipt.
6. **Role-specific dashboards:** dashboard totals include total, active, accepted, and delivered records, scoped to the signed-in donor, recipient, or administrator.
7. **Administration:** admins verify donor and recipient organizations; verified organizations can publish listings and requests and send contributions without further approval.
8. **Database-backed UI roles:** enabled role options and registration permission flags are returned by `GET /api/roles` from the `Roles` table.

## Database and API

The API uses `HungerDbContext` and EF Core SQL Server for controller data access. Frequently used read paths are defined in `Data/HungerQueries.cs` using compiled EF queries. On startup, `DatabaseInitializer` ensures the configured database and these tables exist:

- `Roles` — enabled portal roles and registration permissions.
- `Accounts` — donor, recipient, and administrator profiles with password hashes.
- `VerificationDocuments` — private proof file bytes (`Contents`, `varbinary(max)`) and validated content type keyed to the owning account; regular account/list queries do not load these bytes.
- `Donations`, `DonationRequests`, and `DonationStatusHistory` — listings, recipient claims, and tracking events.
- `CommunityNeeds` and `NeedContributions` — recipient requests, partial donations, moderation, and receipt.

The database connection and administrator credentials are stored outside source control in .NET User Secrets. In this setup, Hunger data is in `db` under the `dbo` schema. In Visual Studio, open **View → SQL Server Object Explorer → (localdb)\MSSQLLocalDB → Databases → db → Tables**. The previous `HungerDb` database is left untouched; its rows are not copied automatically.

To inspect the stored data, run:

```sql
USE db;
SELECT RoleKey, DisplayName, RegistrationAllowed FROM dbo.Roles;
SELECT Name, Email, OrganizationName, Role, VerificationStatus FROM dbo.Accounts;
SELECT Title, Type, Quantity, Status, ReviewStatus FROM dbo.Donations;
SELECT Title, Quantity, Unit, Status, ReviewStatus FROM dbo.CommunityNeeds;
```

### API routes

- Auth: Angular community sign-in at `/login`; administrator sign-in at `/admin/login`; API routes `GET /api/roles`, multipart `POST /api/auth/register` (requires organization proof), `POST /api/auth/login`, `GET /api/auth/me`, and `POST /api/auth/logout`.
- Dashboard/listings: `GET /api/dashboard`; `GET /api/donations`; `GET /api/donations/{id}`; `POST /api/donations`; `PUT` and `DELETE /api/donations/{id}`.
- Donation workflow: `POST /api/donations/{id}/request`; `POST /api/donations/{id}/accept`; `PATCH /api/donations/{id}/status`; `GET /api/donations/{id}/history`.
- Recipient needs: `GET` and `POST /api/needs`; `PUT` and `DELETE /api/needs/{id}`; `GET /api/needs/{id}/contributions`; `POST /api/needs/{id}/contributions`; `POST /api/contributions/{id}/received`.
- Admin identity verification: `GET /api/admin/accounts`; admin-only `GET /api/admin/accounts/{id}/verification-document` to download private proof; `POST /api/admin/accounts/{id}/review`. Verified donor and recipient organizations can publish listings and requests and submit contributions without separate review.
- Health: `GET /api/health`.

Swagger UI and its OpenAPI document are enabled only in the Development environment at `/swagger` and `/swagger/v1/swagger.json`.

## Final implementation plan

### Completed — pilot MVP

- Product branding and separate Angular/API projects with Visual Studio multi-startup configuration.
- Database-backed role options, donor/recipient registration, login/logout, password hashing, and protected routes.
- Admin seeding, organization/listing/request/contribution review, and role-specific permissions.
- Food, grocery, and stationery listing creation/edit/delete; request/accept, partial contribution, cancellation, pickup/delivery status, and history.
- Recipient need creation/edit/delete, use-by date tracking for food and groceries, and confirmation of successful receipt.
- Summary cards and responsive donor, recipient, and admin dashboards; centralized keyed UI copy.
- EF Core SQL Server data access, compiled read queries, LocalDB schema initialization, and Development Swagger.

### Next — before a public pilot

1. Replace demo admin credentials with deployment secrets; implement account recovery, email verification, login throttling, and security monitoring.
2. Agree and publish local food handling, eligibility, collection, allergen disclosure, expiry, and incident-reporting policies with qualified partners.
3. Add notifications for new requests, acceptance, pickup deadlines, cancellations, and delivery confirmation.
4. Add recipient capacity/preferences, donor search filters, and pickup coordination/contact details revealed only after acceptance.
5. Pilot with a small group of donors and nonprofits; test accessibility, mobile use, backups, audit retention, and support procedures.

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

**Important:** this is a functional development MVP, not yet a production-ready food safety or identity-verification system. Uploaded proof is limited to PDF/JPEG/PNG files up to 5 MB and remains in the configured SQL database; administrator approval is a manual review, not independent identity validation. Coordinate food safety, privacy, retention, and NGO vetting with local experts before launch.
