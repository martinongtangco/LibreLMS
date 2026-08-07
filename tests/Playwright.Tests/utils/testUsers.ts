/**
 * Seeded test user credentials.
 *
 * These match the data seeded by EnrollmentSeeder.cs and ManagementSeeder.cs.
 */
export interface TestUser {
  email: string;
  password: string;
  role: 'SuperUser' | 'OrgAdmin' | 'Learner';
  name: string;
}

export const testUsers: Record<string, TestUser> = {
  superUser: {
    email: 'admin@librelms.local',
    password: 'Admin@12345',
    role: 'SuperUser',
    name: 'System Administrator',
  },
  orgAdmin: {
    email: 'admin@example.com',
    password: 'password123',
    role: 'OrgAdmin',
    name: 'Admin User',
  },
  learner: {
    email: 'alice@example.com',
    password: 'password123',
    role: 'Learner',
    name: 'Alice Johnson',
  },
  learnerBob: {
    email: 'bob@example.com',
    password: 'password123',
    role: 'Learner',
    name: 'Bob Smith',
  },
  learnerCarol: {
    email: 'carol@example.com',
    password: 'password123',
    role: 'Learner',
    name: 'Carol Davis',
  },
};
