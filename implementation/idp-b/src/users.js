export const users = [
  { username: 'b.alex', password: 'pass-b-alex', sub: 'B-001', displayName: 'Alex Kim', status: 'active' },
  { username: 'b.jamie', password: 'pass-b-jamie', sub: 'B-002', displayName: 'Jamie Tan', status: 'active' },
  { username: 'b.riley', password: 'pass-b-riley', sub: 'B-003', displayName: 'Riley Ng', status: 'deactivated' }
];

export function findUser(username) {
  return users.find(u => u.username === username);
}
