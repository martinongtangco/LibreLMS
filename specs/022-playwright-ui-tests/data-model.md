# Data Model: Playwright E2E Test Fixtures

This document describes the test data and fixture structures used by the Playwright test suite. No new database entities are created — tests consume the seeded data from the existing application.

## Seeded Test Users

| Email | Password | Role | Name |
|-------|----------|------|------|
| `admin@librelms.local` | `Admin@12345` | SuperUser | System Administrator |
| `admin@example.com` | `password123` | OrgAdmin | Admin User |
| `alice@example.com` | `password123` | Learner | Alice Johnson |
| `bob@example.com` | `password123` | Learner | Bob Smith |
| `carol@example.com` | `password123` | Learner | Carol Davis |

**Source**: `EnrollmentSeeder.cs` (Learners + OrgAdmin) and `ManagementSeeder.cs` (SuperUser)

## Seeded Courses (10 total)

| Title | Category | Course ID |
|-------|----------|-----------|
| Introduction to C# | Programming | `11111111-1111-1111-1111-111111111111` |
| Advanced .NET Patterns | Programming | `11111111-1111-1111-1111-111111111112` |
| Web Development with ASP.NET Core | Programming | `11111111-1111-1111-1111-111111111113` |
| Database Design Fundamentals | Database | `11111111-1111-1111-1111-111111111114` |
| UI/UX Design Principles | Design | `11111111-1111-1111-1111-111111111115` |
| Responsive Web Design | Design | `11111111-1111-1111-1111-111111111116` |
| Git Version Control | Tools | `11111111-1111-1111-1111-111111111117` |
| Docker and Container Basics | Tools | `11111111-1111-1111-1111-111111111118` |
| Introduction to SQL | Database | `11111111-1111-1111-1111-111111111119` |
| REST API Design | Programming | `11111111-1111-1111-1111-111111111120` |

**Source**: `CatalogSeeder.cs`

## Categories

- Programming (4 courses)
- Database (2 courses)
- Design (2 courses)
- Tools (2 courses)

## Seeded Enrollments

| Student | Course |
|---------|--------|
| Alice Johnson | Introduction to C# (SCORM course) |

**Source**: `EnrollmentSeeder.cs`

## Test Fixture Structure

### `TestUsers` (TypeScript interface)

```typescript
interface TestUser {
  email: string;
  password: string;
  role: 'SuperUser' | 'OrgAdmin' | 'Learner';
  name: string;
}
```

### Authenticated Fixture

Each test file receives a `page` fixture that is pre-authenticated as a specific role. The fixture:
1. Navigates to `/Account/Login`
2. Fills in email and password
3. Submits the form
4. Waits for redirect to `/` → `/Courses/Index`
5. Returns the authenticated `page` instance

### Page Object Base Class

```typescript
class BasePage {
  constructor(protected page: Page) {}
  // Common utilities: waitForHtmxSettle(), waitForNavigation(), etc.
}
```

## Validation Rules

- **Email**: Must match exactly (case-sensitive) as seeded
- **Password**: Must match exactly (case-sensitive) as seeded
- **Course titles**: Used in assertions via partial match (e.g., "Introduction to C#")
- **User names**: Displayed in the account control; used in assertions (e.g., "Alice Johnson")
