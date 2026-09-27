import { Brand } from '../components/Brand';
import { useState } from 'react';
import type { FormEvent } from 'react';
import { useMutation } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { toast } from 'sonner';
import { loginMutation } from '../services/api/generated/@tanstack/react-query.gen';
import { setSession } from '../services/auth/session';

export function LoginPage() {
  const navigate = useNavigate();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');

  const login = useMutation({
    ...loginMutation(),
    onSuccess: (tokens) => {
      setSession(tokens);
      toast.success(`Signed in as ${tokens.principal.display_name}`);
      navigate('/', { replace: true });
    },
  });

  function submit(event: FormEvent) {
    event.preventDefault();
    login.mutate({ body: { email, password } });
  }

  return (
    <main className="login-page">
      <div className="card">
        <Brand />
        <h1 className="auth-title">Welcome back</h1>
        <p className="muted">Sign in to your CareLanka staff workspace.</p>

        <form onSubmit={submit} style={{ marginTop: '1.25rem' }}>
          <div className="field">
            <label htmlFor="email">Email</label>
            <input
              id="email"
              type="email"
              autoComplete="username"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              required
            />
          </div>

          <div className="field">
            <label htmlFor="password">Password</label>
            <input
              id="password"
              type="password"
              autoComplete="current-password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              required
            />
          </div>

          <button type="submit" disabled={login.isPending} style={{ width: '100%' }}>
            {login.isPending ? 'Signing in…' : 'Sign in'}
          </button>
        </form>

        <p className="hint">
          Patients can sign in or register using the CareLanka mobile app.
        </p>
      </div>
    </main>
  );
}
