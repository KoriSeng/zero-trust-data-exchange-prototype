export const users = [
  { username: 'b.alex', password: 'pass-b-alex', sub: 'B-001', displayName: 'Alex Kim', email: 'b.alex@example.com', status: 'active' },
  { username: 'b.jamie', password: 'pass-b-jamie', sub: 'B-002', displayName: 'Jamie Tan', email: 'b.jamie@example.com', status: 'active' },
  { username: 'b.riley', password: 'pass-b-riley', sub: 'B-003', displayName: 'Riley Ng', email: 'b.riley@example.com', status: 'deactivated' }
];

export function findUser(username) {
  return users.find(u => u.username === username);
}
