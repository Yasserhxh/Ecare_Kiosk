# Ecare — SSO + RBAC (profils guichet commercial) — Design

- **Date:** 2026-09-24
- **Author:** Yasser Bouaabane (with Claude Code)
- **Scope:** `Ecare_Kiosk` (backend API) + `Ecare-FrontUi` (React admin UI) + a SQL role script.
  `mycimar-web-client` is touched **only** by the SQL role insert — no page/code changes there.
- **Source of requirements:** `Copie de Profil guichet commercial.xlsx` (sheets: Matrice Accès, Détail Fonctions, Agents Concernés).

## 1. Goal

1. **SSO** — **add** Azure AD (Entra) SSO login to Ecare *alongside* the existing username/password
   login (not a replacement), replicating the working pattern in `mycimar-web-client`
   (Microsoft.Identity.Web OpenID Connect, match Azure email against the shared `AspNetUsers`, roles
   from the shared `AspNetUserRoles`). Password login keeps working exactly as today. Excel row R14
   *"désactivation auth pwd"* is therefore an **optional, deferred** switch, not the delivered end
   state — both auth methods coexist.
2. **RBAC** — enforce the read/write permission matrix (Sheet "Matrice Accès") across four profils:
   **Agent de Guichet**, **Logistique**, **Expédition**, **Admin IT** — on Ecare_Kiosk endpoints and
   in the Ecare-FrontUi. **Enforcement applies to SSO-authenticated sessions only** for this delivery;
   password sessions keep today's unrestricted behavior (see Decisions). RBAC becomes universal later
   when password login is retired.

## 2. Key facts established during exploration

- **Shared database.** `Ecare_Kiosk` and `mycimar-web-client` both connect to
  `sql-emea-we-dssprod-dss-001 / sqldb-emea-we-dssprod-dss-001`. `AspNetUsers` / `AspNetRoles` /
  `AspNetUserRoles` are the **same physical tables**. No user sync is required — a user created by
  Admin IT in mycimar-web-client is immediately visible to Ecare.
- **Identity ownership.** Admin IT creates/edits users in `mycimar-web-client`
  (`EmployéesController` → `Ajouter` / `Modifier` / `ChangerRole`, guarded by
  `[Authorize(Roles = "Admin IT")]`; views `Views/Employées/{Index,Ajouter,Modifier}.cshtml`).
  Ecare **only consumes** users — it never creates them.
- **Ville / activité scoping model (from mycimar).** A user's ville = `ApplicationUser.Id_Ville`
  (FK → `Ville`). A user's activité is derived from their **role** via `AspNetRoles_Activités`
  (Role_Id → Activité_Id → `Activite.Service`, e.g. "Ciments"). mycimar injects a `VilleId` **claim**
  at login and filters data by it.
- **Ecare does NOT auto-migrate.** No `Database.Migrate()` / `EnsureCreated()` at startup. There is an
  `InitialCreate` migration but it is not applied automatically. Ecare's `ApplicationUser`
  (`IdentityUser<string>` with `Prenom/Nom/IsActive/RaisonSociale`) is a **subset** of mycimar's
  `ApplicationUser` (which adds `Id_Ville`, `VilleId`, `Region`, `Affectations`, …). **Constraint:**
  never run Ecare's Identity migration against the shared prod DB — it would drop mycimar's columns.
  Roles are added via the SQL script only (Section 8).
- **Current Ecare auth.** JWT bearer (`AddJwtBearer`, `Jwt:Issuer=EcareApi`, `Jwt:Audience=EcareClients`,
  365-day tokens) + ASP.NET Identity. Login: `POST /auth/login` → `LoginHandler` → `JwtTokenService`
  returns `AuthResponse(Token, UserName, Email, Role, UserId, ExpiresAt)`. Only `/auth/deactivate` and
  `/auth/users` are `[Authorize(Roles="Admin")]`; **all other endpoints are open**.
- **Current Ecare frontend.** React + Vite + TS. `authService` stores JWT in `localStorage`
  (`admin_token`, `admin_user`); `Authorization: Bearer` attached in `src/api/auth.ts`; on 401 it
  clears and redirects to `/admin/login`. `RequireAuth` exists but is **commented out** in
  `PrivateRoutes.tsx` — routes are currently unguarded. No MSAL. `AdminLoginPage.tsx` is a
  username/password form.
- **mycimar SSO blueprint (to replicate).** `CimarPortailClient/Program.cs`:
  `AddAuthentication(DefaultScheme="Cookies").AddMicrosoftIdentityWebApp(...)` with
  `SignInScheme = IdentityConstants.ExternalScheme`, `CallbackPath=/signin-oidc`,
  `SignedOutCallbackPath=/signout-oidc`; packages `Microsoft.Identity.Web` 3.0.1 +
  `Microsoft.AspNetCore.Authentication.OpenIdConnect` 8.0.0. `AccountController.B2CLogin` →
  `Challenge("OpenIdConnect")`; `ExternalLoginCallback` extracts email
  (`preferred_username` → `ClaimTypes.Email` → `ClaimTypes.Upn` → identity name), `FindByEmailAsync`,
  fails if user not found, `SignInAsync`, reads roles, routes by role. Config `AzureAd`
  (Instance, TenantID, ClientID, CallbackUrl, SigoutcallbackUrl); secret in App Service settings.

## 3. Decisions (locked)

- **Handoff approach: A** (server-side OIDC on the Ecare API → mint the existing Ecare JWT), with
  **one-time-code** delivery to the SPA (JWT never placed in a URL fragment / browser history).
- **Role model:** generic profil roles + ville scoping (4 roles, ville/activité orthogonal).
- **Permission matrix storage:** in code (seeded authorization policies), single source of truth.
- **Role names (exact strings):** `Agent de Guichet`, `Logistique`, `Expédition`, `Admin IT`
  (Admin IT already exists — reused).
- **Password vs SSO enforcement:** password logins are **exempt** from RBAC (full/legacy access,
  `authmethod=pwd`); SSO logins are **enforced** (`authmethod=sso` + `perms`). Future: retire password
  login → RBAC universal.
- **Admin role equivalence:** the web-client catalog has both `Admin` and `Admin IT`. Both map to the
  **Admin IT** permission set in Ecare.

## 4. The permission matrix (from Sheet "Matrice Accès")

`R` = read allowed, `W` = write allowed. Empty = denied.

| # | Fonction | Permission key(s) | Guichet | Logistique | Expédition | Admin IT |
|---|---|---|---|---|---|---|
| 1 | Commandes – Création/Valider/Annulation | `Commands.Read/Write` | R W | R W | — | R |
| 2 | Commandes – Correction des quantités | `CommandsQuantities.Read/Write` | R W | R W | — | R |
| 3 | Flux logistique – Lecture statut | `FluxStatus.Read` | R | R | R | R |
| 4 | Force Call / file d'attente | `ForceCall.Read/Execute` | R | R W | R | R |
| 5 | Données logistique – Création camions/chauffeurs | `LogisticsData.Read/Write` | R | R W | R | R |
| 6 | RFID – Affectations | `Rfid.Read/Write` | R | R W | R | R |
| 7 | Ensachage – Dispo articles & postes | `Bagging.Read/Write` | R | R | R W | R W |
| 8 | Points de chargement – Quotas | `LoadingQuotas.Read/Write` | R | R | R W | R |
| 9 | Rapports journalier | `Reports.Read` | R | R | R | R |
| 10 | Pesage / Pont-bascule – Lecture | `Weighing.Read` | R | R | R | R |
| 11 | Rapport traçabilité (logs appli) | `TraceabilityLogs.Read` | — | — | — | R |
| 12 | Expédition / réglage chargement (plombe, nb sac) | `LoadingSettings.Read/Write` | R | R | R W | R |

Notes:
- The sheet's *Activation SSO CIMARFLOW* row (R14) is not a per-profil permission — it is the global
  auth switch (Section 5, `Auth:PasswordLoginEnabled`), so it is not in this 12-row table.
- The write columns for read-only rows (Flux, Reports, Weighing) are denied for all profils.
- This table is the authoritative encoding. The unit test in Section 9 asserts the code map equals it
  cell-by-cell.

## 5. SSO wiring — Ecare_Kiosk API

**Packages** (add to `Ecare.Api` / `Ecare.Infrastructure`):
`Microsoft.Identity.Web` (3.x) and `Microsoft.AspNetCore.Authentication.OpenIdConnect` (8.0). The API
already references `Microsoft.Identity.Client` (Graph mail).

**Config** — add to `appsettings.json` (values per environment; **secret only in App Service settings**,
double-underscore `AzureAd__ClientSecret`):
```json
"AzureAd": {
  "Instance": "https://login.microsoftonline.com",
  "TenantID": "<same tenant as mycimar for the target env>",
  "ClientID": "<Ecare app registration or reused mycimar client id>",
  "CallbackUrl": "/signin-oidc",
  "SigoutcallbackUrl": "/signout-oidc"
},
"Spa": { "BaseUrl": "<Ecare-FrontUi origin>" },
"Auth": { "PasswordLoginEnabled": true }
```

**Both login methods coexist, but they behave differently (by design):**
- `POST /auth/login` (**password**) stays fully functional and unchanged. Its JWT carries
  `authmethod=pwd` and is treated as **full/legacy access** — the RBAC matrix is **not** enforced, so
  password users get "the system how it is normally" (today's behavior).
- `GET /auth/sso/login` (**SSO**) is the new path. Its JWT carries `authmethod=sso`, `role`, `VilleId`,
  `activite`, and the `perms` array — and the **RBAC matrix is enforced**.

`Auth:PasswordLoginEnabled` defaults to `true`. The intended **future** end-state (not this delivery) is
to set it `false` for Ecare (and retire password login for guichet commercial in web-client), at which
point RBAC becomes universal because only SSO sessions remain.

**Program.cs** — two coexisting schemes:
- `JwtBearer` stays the **default** authenticate/challenge scheme (protects `/…` API calls) — unchanged.
- Add a **cookie + OpenIdConnect** pair used only for the login dance, via `AddMicrosoftIdentityWebApp`
  under an explicit scheme name (mirrors mycimar's block, `SignInScheme = ExternalScheme`,
  `CallbackPath=/signin-oidc`, `SignedOutCallbackPath=/signout-oidc`). This cookie is transient and used
  only between `Challenge` and the callback.

**Endpoints** (new, in `AuthEndpoints.cs` or a new `SsoEndpoints.cs`):
- `GET /auth/sso/login?returnUrl=` → `Challenge` the OIDC scheme (redirect to Azure AD).
- `GET /signin-oidc` → handled by Microsoft.Identity.Web middleware; then a post-auth callback endpoint:
  1. read the authenticated principal from the transient cookie,
  2. extract email (`preferred_username` → `email` → `upn` → name — same fallback as mycimar),
  3. `UserManager.FindByEmailAsync`; if null or `!IsActive` → redirect to
     `{Spa:BaseUrl}/auth/callback?error=denied`,
  4. `GetRolesAsync`; resolve the single Ecare profil role,
  5. resolve `VilleId` (`ApplicationUser.Id_Ville`) and `activité`
     (`AspNetRoles_Activités` for the role),
  6. mint the Ecare JWT via `JwtTokenService`, adding claims `authmethod=sso`, `role`, `VilleId`,
     `activite`, and a `perms` array (from the permission map); the existing `POST /auth/login` path is
     updated to add `authmethod=pwd` (and no `perms`, since password sessions are exempt),
  7. store a short-lived (~60 s) **one-time code → JWT** entry (in-memory `IMemoryCache`, single-use),
  8. sign out the transient cookie and redirect to `{Spa:BaseUrl}/auth/callback?code=<code>`.
- `POST /auth/sso/exchange` `{ code }` → validates + consumes the code, returns
  `{ token, userName, email, role, perms, expiresAt }` (extends the current `AuthResponse`).
- `GET /auth/sso/logout` → cookie sign-out + Azure AD sign-out (`/signout-oidc`).

**Password login:** `POST /auth/login` stays **enabled and unchanged** (`Auth:PasswordLoginEnabled`
default `true`) — password and SSO coexist. The flag only allows turning password login off in the
future (R14); when off, `/auth/login` returns disabled.

## 6. Role & permission model — Ecare_Kiosk (in code)

New files in `Ecare.Application` (or a small `Ecare.Security` area):
- `EcareRoles` — string constants for the 4 role names (exact wording from Section 3).
- `Permissions` — string constants for every key in the Section-4 table.
- `PermissionMatrix` — `IReadOnlyDictionary<string, IReadOnlySet<string>>` mapping each role to its
  permission set, encoding the Section-4 table exactly. **Single source of truth.** `Admin` and
  `Admin IT` both resolve to the Admin IT set.
- `PermissionRequirement : IAuthorizationRequirement` (+ `PermissionAuthorizationHandler`) with this
  logic: (a) if the token's `authmethod == pwd` → **succeed** (password sessions are exempt this
  delivery); (b) if `authmethod == sso` → succeed only when the required permission is in the caller's
  `perms` (equivalently, in `PermissionMatrix[role]`). No token → the underlying
  `RequireAuthorization` returns 401.
- Startup: register one authorization **policy per permission** named `Perm:<key>` (loop over
  `Permissions`), each backed by a `PermissionRequirement`. Alternatively a single policy provider that
  materializes `Perm:*` policies on demand.

The `perms` array is computed once from `PermissionMatrix[role]` and embedded in the JWT so the SPA and
API share the exact same set without the SPA re-encoding the matrix.

## 7. Backend enforcement — Ecare_Kiosk endpoints

- Add `.RequireAuthorization("Perm:<key>")` to the **agent/admin-facing** endpoint groups, mapping each
  route to its matrix function. Because password tokens are exempt in the handler, this is transparent to
  password users (they still pass) while SSO users are enforced; a caller with **no** token gets 401
  (the React app always holds a token, so this matches today's logged-in behavior). Target groups (from
  `Endpoints/*.cs`): `Order`, `Queue`, `Flux`, `Pab`, `Device`, `Ligne`, `Other`. Exact
  route→permission assignments are finalized in the implementation
  plan by reading each endpoint; the mapping follows Section 4 (e.g. order create/validate/cancel →
  `Perm:Commands.Write`; quantity correction → `Perm:CommandsQuantities.Write`; queue force-call →
  `Perm:ForceCall.Execute`; camion/chauffeur create → `Perm:LogisticsData.Write`; RFID affectation →
  `Perm:Rfid.Write`; loading settings (plombe / nb sac) → `Perm:LoadingSettings.Write`; quotas →
  `Perm:LoadingQuotas.*`; flux/weighing/reports → their `.Read`; traceability logs →
  `Perm:TraceabilityLogs.Read`).
- **Kiosk-hardware carve-out (critical).** The unattended physical-kiosk flows (RFID scan, SignalR
  device pushes — `Kiosk` endpoints and device hubs) are machine-driven, not human-profil actions, and
  must **not** be gated by agent permissions. They stay on their own path (open, or a dedicated
  device/service token). The matrix governs *guichet agents* only.
- **Ville / activité scoping.** `VilleId` + `activite` claims travel in the JWT. Data reads are filtered
  by them **where Ecare's model has a site dimension**. Ecare is currently Temara/Ciment-centric, so the
  claims + filtering hooks are scaffolded now and switched fully on as Ecare serves multiple sites
  (YAGNI). This mirrors mycimar's `VilleId`-claim approach.

## 8. SQL deliverable (run against the shared prod DB — no EF migration)

Idempotent script (`docs/superpowers/specs/sql/2026-09-24-ecare-roles.sql`) that:
1. Inserts the three new roles into `AspNetRoles` (`Agent de Guichet`, `Logistique`, `Expédition`),
   each `IF NOT EXISTS`, with `Id = NEWID()` (string), `Name`, `NormalizedName = UPPER(Name)`,
   `ConcurrencyStamp = NEWID()`. `Admin IT` is skipped (already present).
2. Links each of the four roles to the Ciments activité in `AspNetRoles_Activités`
   (`Activité_Id = (SELECT Id FROM Activite WHERE Service LIKE 'Ciment%')`), `IF NOT EXISTS`, so the
   roles appear under activité = Ciment in mycimar's `GetRolesParActivité` dropdown.

Optional: mirror the same 4 roles into `Repository/Data/AppDbInitializer.SeedRolesAsync` so a re-seed
doesn't drop them. Not required for delivery.

## 9. Frontend — Ecare-FrontUi

- **Login page** (`AdminLoginPage.tsx`): replace the username/password form with a
  **"Se connecter avec SSO"** button → `window.location.href = ${API_BASE}/auth/sso/login`.
- **New route** `/auth/callback`: reads `code` (or `error`), calls `POST /auth/sso/exchange`, stores
  `{ token, user, perms }` in the existing `localStorage` keys, redirects to the app; on `error=denied`
  shows the mycimar-style "La connexion a échoué / accès non autorisé" message.
- **`authService`**: add `getAuthMethod()`, `getPermissions(): string[]`, and
  `hasPermission(key): boolean` where **`authmethod=pwd` ⇒ `hasPermission` returns `true` for
  everything** (password users see the full UI, matching backend exemption); `authmethod=sso` ⇒ checks
  the `perms` array.
- **Route protection**: re-enable `RequireAuth` in `PrivateRoutes.tsx` and add a permission-aware guard
  (`RequirePermission` wrapper) + conditional rendering to hide/disable actions per `hasPermission`.
  Password sessions pass all permission checks; SSO sessions are gated per the matrix.
- **401 handling** in `src/api/auth.ts`: repoint the redirect to the SSO login.

## 10. Error handling

- SSO callback for a user absent from `AspNetUsers`, inactive, or without an Ecare profil role →
  redirect to `{Spa}/auth/callback?error=denied` with a friendly French message (mirrors mycimar).
- One-time code expired/reused → `exchange` returns 400; SPA restarts the SSO flow.
- Missing `AzureAd` config → API logs and disables the SSO endpoints (does not crash); if
  `PasswordLoginEnabled` is also false this is a deployment error surfaced clearly at startup.

## 11. Testing

- **Permission map test** — asserts `PermissionMatrix` equals the Section-4 table cell-by-cell (the
  guard against silent drift).
- **Authorization handler tests** — role X + permission Y → allow/deny per matrix; `authmethod=pwd`
  token → always allowed (exempt); `authmethod=sso` token → enforced; `Admin` and `Admin IT` both get
  the Admin IT set.
- **SSO callback handler test** — faked `UserManager`: email→user→JWT happy path; user-not-found,
  inactive, and no-role paths → denied redirect.
- **One-time-code exchange test** — single-use, expiry.
- **Frontend** — `RequirePermission` guard and conditional-render tests; callback route stores token.

## 12. Rollout order

1. Run the role SQL (Section 8) against the shared prod DB.
2. Register the Ecare `/signin-oidc` redirect URI in the Azure AD app (reuse mycimar's app registration
   or a new one — ops), and set `AzureAd__ClientId` / `__TenantId` / `__ClientSecret` in the Ecare App
   Service settings.
3. Admin IT assigns users to the new roles via mycimar's `Employées` pages.
4. Deploy Ecare_Kiosk (SSO endpoints live, `PasswordLoginEnabled` still `true`).
5. Deploy Ecare-FrontUi (SSO login button added; password form kept).
6. *(Optional, deferred — not part of this delivery)* Flip `Auth:PasswordLoginEnabled = false` to
   complete R14, only if/when the business decides to retire password login.

## 13. Out of scope

- No changes to mycimar-web-client code/pages (SQL role insert only).
- No modification of the shared Identity schema (no EF migration to prod).
- Full multi-site ville/activité data filtering beyond scaffolding (Ecare is Temara/Ciment today).
- Key rotation / App Service settings hardening tracked separately (see gitleaks remediation notes).
