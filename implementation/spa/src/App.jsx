import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import AppShell from './components/AppShell';
import RequireAuth from './components/RequireAuth';
import { AuthProvider } from './contexts/AuthContext';
import Callback from './pages/Callback';
import Dashboard from './pages/Dashboard';
import DatasetExplorer from './pages/DatasetExplorer';
import Login from './pages/Login';
import MyRequests from './pages/MyRequests';
import PendingApprovals from './pages/PendingApprovals';

export default function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <Routes>
          <Route path="/login" element={<Login />} />
          <Route path="/callback" element={<Callback />} />

          <Route
            element={(
              <RequireAuth>
                <AppShell />
              </RequireAuth>
            )}
          >
            <Route path="/" element={<Dashboard />} />
            <Route path="/requests" element={<MyRequests />} />
            <Route path="/datasets" element={<DatasetExplorer />} />
            <Route path="/approvals" element={<PendingApprovals />} />
          </Route>

          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  );
}
