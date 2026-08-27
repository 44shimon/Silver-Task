import { useEffect } from 'react';
import { useLocation } from 'react-router-dom';

const STORAGE_KEY = 'silvertask:lastVisitedPage';

// Deliberately localStorage, not a server-side preference — "last visited page" is a genuinely
// browser-local concept (a user logging in on a second device shouldn't be yanked to wherever
// their *other* device last was); see UserPreference.DefaultLandingPage's own doc comment on why
// only this one option resolves client-side while "Dashboard"/"MyTasks" are plain server-stored
// route names. Excludes the landing/login routes themselves so "last visited" never points back
// at the redirect that got you there.
const EXCLUDED_PREFIXES = ['/login'];

export function useLastVisitedPage() {
  const location = useLocation();

  useEffect(() => {
    if (EXCLUDED_PREFIXES.some((prefix) => location.pathname.startsWith(prefix))) {
      return;
    }
    localStorage.setItem(STORAGE_KEY, location.pathname + location.search);
  }, [location.pathname, location.search]);
}

export function getLastVisitedPage(): string | null {
  return localStorage.getItem(STORAGE_KEY);
}
