export const users = [
  { username: 'a.alex', password: 'pass-a-alex', sub: 'A-001', displayName: 'Alex Kim', email: 'a.alex@example.com', status: 'active' },
  { username: 'a.sam', password: 'pass-a-sam', sub: 'A-002', displayName: 'Sam Lee', email: 'a.sam@example.com', status: 'active' },
  { username: 'a.pat', password: 'pass-a-pat', sub: 'A-003', displayName: 'Pat Chen', email: 'a.pat@example.com', status: 'deactivated' }
];

export function findUser(username) {
  return users.find(u => u.username === username);
}
