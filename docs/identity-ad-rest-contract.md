# Identity provider: Active Directory over REST (AD-REST)

AuditX authenticates users against the bank's Active Directory. It ships with three interchangeable
identity providers, selected by the `Identity:Provider` setting:

| `Identity:Provider`  | Provider                     | When to use                                                        |
| -------------------- | ---------------------------- | ------------------------------------------------------------------ |
| `Development`        | Seeded in-memory users       | Local development / demo — no domain required.                     |
| `ActiveDirectory`    | LDAPS bind + service account | You have direct LDAPS connectivity and a service account.          |
| `ActiveDirectoryApi` | **The bank's AD REST gateway** | AD is exposed only through an internal HTTP API (common at NG banks). |

AuditX **never stores passwords**. For AD-REST it forwards a submitted password exactly once to the
gateway's credential-validation endpoint and keeps only the returned directory attributes. Authorization
(roles, permissions) remains internal to AuditX and independent of AD groups — the directory is used for
authentication and optional coarse-grained provisioning gating only.

## Configuration

```jsonc
"Identity": { "Provider": "ActiveDirectoryApi", "AdReverificationMinutes": 5 },
"ActiveDirectoryApi": {
  "BaseUrl": "https://ad-gateway.bank.internal/api/v1", // required
  "AuthenticatePath": "/authenticate",                  // POST  {username,password} -> user | 401
  "LookupPath": "/users/{account}",                     // GET   by account name     -> user | 404
  "StatusPath": "/users/by-sid/{sid}",                  // GET   by objectSid         -> user | 404
  "ApiKeyHeader": "X-Api-Key",                          // service credential header (omit to send none)
  "ApiKey": "<gateway service key>",                    // set via env/secret, not in source control
  "TimeoutSeconds": 15
}
```

`{account}` and `{sid}` are URL-encoded and substituted into the path templates. The API key header is sent
on every request. All calls are expected over HTTPS on the bank's internal network.

## The contract the gateway must implement

### 1. Credential validation — `POST {BaseUrl}{AuthenticatePath}`

Request body:

```json
{ "username": "jdoe", "password": "••••••••" }
```

* **Valid credentials →** `200 OK` with a [directory-user object](#directory-user-object).
* **Invalid credentials →** `401 Unauthorized` or `403 Forbidden` (AuditX treats both as "login failed").
* Any other status, a timeout, or an unreachable gateway is treated as a **failed** login (fail-closed).

### 2. Lookup by account name — `GET {BaseUrl}{LookupPath}`

Returns the [directory-user object](#directory-user-object) for the given `sAMAccountName` or UPN, or
`404 Not Found` if unknown. Used by SSO and by the provisioning group/OU filter.

### 3. Status by objectSid — `GET {BaseUrl}{StatusPath}`

Returns a [directory-user object](#directory-user-object) (at minimum its `enabled` flag) for the given
`objectSid`, or `404`. Called on session refresh to re-verify the account is still present and enabled
(`AdReverificationMinutes`).

### Directory-user object

The response body for all three endpoints. Property names are matched case-insensitively.

```json
{
  "samAccountName": "jdoe",
  "userPrincipalName": "jdoe@bank.local",
  "objectSid": "S-1-5-21-1111111111-2222222222-3333333333-1001",
  "email": "jdoe@bank.com",
  "firstName": "John",
  "lastName": "Doe",
  "displayName": "John Doe",
  "enabled": true,
  "distinguishedName": "CN=John Doe,OU=Audit,DC=bank,DC=local",
  "groups": ["CN=Auditors,OU=Groups,DC=bank,DC=local", "CN=Staff,OU=Groups,DC=bank,DC=local"]
}
```

| Field               | Required | Notes                                                                         |
| ------------------- | -------- | ----------------------------------------------------------------------------- |
| `samAccountName`    | yes      | Stored as the account identity.                                               |
| `userPrincipalName` | yes      | Used for UPN-style login and matching.                                        |
| `objectSid`         | yes      | The stable directory key AuditX pins the account to (survives renames).       |
| `email`             | yes      | User's mailbox.                                                               |
| `firstName` / `lastName` | yes | Names for display / JIT provisioning.                                          |
| `displayName`       | no       | Falls back to `firstName lastName` if omitted.                                |
| `enabled`           | yes      | `false` blocks login and revokes the session on next re-verification.         |
| `distinguishedName` | no       | Only needed if you use the OU provisioning filter.                            |
| `groups`            | no       | AD group identifiers (SID, DN or name) — only needed for the group filter.    |

## Optional provisioning filter (US-M1-006)

If a coarse-grained provisioning filter is configured (an OU DN and/or a group identifier), AuditX gates
**first-login provisioning** on it:

* **OU filter** — the user's `distinguishedName` must end with the configured OU DN.
* **Group filter** — the configured group identifier must appear in the user's `groups` (exact,
  case-insensitive match — provide it in whatever form your gateway emits: SID, DN or name).

With no filter configured, every directory user who authenticates is eligible for provisioning (and lands
in `awaiting_role_assignment` with no permissions until an administrator grants a role).

## Security notes

* Serve the gateway over HTTPS on the internal network; AuditX sends the password and API key on each call.
* Store `ActiveDirectoryApi:ApiKey` as a deployment secret (environment variable / secret store), never in
  source control.
* The gateway should rate-limit and log authentication attempts; AuditX records its own audit-trail entry
  for every login outcome.
