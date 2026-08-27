import { Navigate } from 'react-router-dom';
import { useUserPreferences } from '@/hooks/useUserSettings';
import { getLastVisitedPage } from '@/hooks/useLastVisitedPage';

// "/" itself is never a real page — it immediately redirects based on
// UserPreference.DefaultLandingPage (Dashboard/MyTasks/LastVisited), matching every prior
// post-login destination (LoginPage.tsx defaults `from` to "/") without needing to touch the
// login flow itself. "LastVisited" resolves from localStorage (see useLastVisitedPage's own doc
// comment on why that one specifically is browser-local, not server-synced).
export function LandingRedirect() {
  const { data: preferences, isLoading } = useUserPreferences();

  if (isLoading) {
    return <div className="auth-loading">Loading...</div>;
  }

  if (preferences?.defaultLandingPage === 'MyTasks') {
    return <Navigate to="/my-tasks" replace />;
  }

  if (preferences?.defaultLandingPage === 'LastVisited') {
    const last = getLastVisitedPage();
    if (last && last !== '/') {
      return <Navigate to={last} replace />;
    }
  }

  return <Navigate to="/dashboard" replace />;
}
