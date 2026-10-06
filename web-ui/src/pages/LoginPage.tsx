import { useState } from 'react';
import type { FormEvent } from 'react';
import { useMutation } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { toast } from 'sonner';
import { Lock, Mail, Stethoscope } from 'lucide-react';
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
      <div className="login-shell">
        <section className="login-intro">
          <div className="login-intro__badge">
            <Stethoscope size={28} aria-hidden="true" />
          </div>
          <h1 className="login-intro__title">Welcome to CareLanka</h1>
          <p className="login-intro__text">
            The hospital workspace for staff — admissions, wards, equipment, the pharmacy and
            the laboratory, all in one place.
          </p>
          <LoginIllustration />
        </section>

        <section className="login-panel">
          <div className="login-panel__card">
            <h2 className="login-panel__heading">Staff Sign In</h2>

            <form onSubmit={submit}>
              <div className="login-field">
                <label htmlFor="email">Email</label>
                <div className="login-field__control">
                  <Mail size={16} aria-hidden="true" className="login-field__icon" />
                  <input
                    id="email"
                    type="email"
                    autoComplete="username"
                    placeholder="you@carelanka.lk"
                    value={email}
                    onChange={(event) => setEmail(event.target.value)}
                    required
                  />
                </div>
              </div>

              <div className="login-field">
                <label htmlFor="password">Password</label>
                <div className="login-field__control">
                  <Lock size={16} aria-hidden="true" className="login-field__icon" />
                  <input
                    id="password"
                    type="password"
                    autoComplete="current-password"
                    placeholder="••••••••"
                    value={password}
                    onChange={(event) => setPassword(event.target.value)}
                    required
                  />
                </div>
              </div>

              <button type="submit" className="login-panel__submit" disabled={login.isPending}>
                {login.isPending ? 'Signing in…' : 'Sign in'}
              </button>
            </form>

            <p className="hint" style={{ textAlign: 'center' }}>
              Patients can sign in or register using the CareLanka mobile app.
            </p>
          </div>
        </section>
      </div>
    </main>
  );
}

// A small, original illustration in the brand palette — not a copy of any reference image.
function LoginIllustration() {
  return (
    <svg
      className="login-intro__art"
      viewBox="0 0 240 160"
      fill="none"
      xmlns="http://www.w3.org/2000/svg"
      aria-hidden="true"
    >
      <rect x="10" y="100" width="220" height="10" rx="5" fill="var(--accent-soft)" />
      <rect x="30" y="60" width="130" height="50" rx="10" fill="#ffffff" stroke="var(--line)" />
      <rect x="30" y="60" width="130" height="14" rx="7" fill="var(--accent)" opacity="0.15" />
      <circle cx="52" cy="67" r="4" fill="var(--accent)" />
      <path
        d="M40 95h18l6-14 8 22 6-14h12"
        stroke="var(--accent)"
        strokeWidth="3"
        strokeLinecap="round"
        strokeLinejoin="round"
        fill="none"
      />
      <rect x="178" y="30" width="14" height="80" rx="4" fill="var(--line)" />
      <circle cx="185" cy="26" r="9" fill="var(--danger, #c0392b)" opacity="0.85" />
      <path d="M185 36v30" stroke="var(--line)" strokeWidth="2" />
      <circle cx="205" cy="120" r="16" fill="var(--accent-soft)" />
      <path d="M205 112v16M197 120h16" stroke="var(--accent)" strokeWidth="3" strokeLinecap="round" />
    </svg>
  );
}
