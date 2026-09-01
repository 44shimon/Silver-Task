import { useState, type FormEvent } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { useLogin } from '@/hooks/useAuth';
import { usePublicSettings } from '@/hooks/useSystemSettings';
import { ApiError } from '@/api/httpClient';
import { VersionFooter } from '@/components/layout/VersionFooter';
import './LoginPage.css';

interface LocationState {
  from?: { pathname: string };
}

export function LoginPage() {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const login = useLogin();
  const navigate = useNavigate();
  const location = useLocation();
  const { data: publicSettings } = usePublicSettings();

  const state = location.state as LocationState | null;
  const from = state?.from?.pathname ?? '/';

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    login.mutate(
      { email, password },
      {
        onSuccess: () => navigate(from, { replace: true }),
      },
    );
  }

  return (
    <div className="login-page">
      <form className="login-card" onSubmit={handleSubmit}>
        <h1>{publicSettings?.applicationName ?? 'Silver-Task'}</h1>
        <p className="login-subtitle">{publicSettings?.applicationDescription ?? 'Sign in to continue'}</p>

        <label className="login-field">
          <span>Email</span>
          <input
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            required
            autoFocus
            autoComplete="email"
          />
        </label>

        <label className="login-field">
          <span>Password</span>
          <input
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
            autoComplete="current-password"
          />
        </label>

        {login.isError && (
          <p className="login-error">
            {login.error instanceof ApiError ? login.error.message : 'Something went wrong. Please try again.'}
          </p>
        )}

        <button type="submit" className="login-submit" disabled={login.isPending}>
          {login.isPending ? 'Signing in...' : 'Sign in'}
        </button>
      </form>
      <VersionFooter />
    </div>
  );
}
