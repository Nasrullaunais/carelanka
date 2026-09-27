import { useSession } from '../services/auth/useSession';
import { roleLabels } from '../types/permissions';
import { ProfileAvatar } from '../components/ProfileAvatar';

export function ProfilePage() {
  const principal = useSession()?.principal;
  if (!principal) return null;
  return <>
    <p className="eyebrow">Account</p>
    <h1>My profile</h1>
    <p className="muted">Your staff account and contact information.</p>
    <section className="card profile-card">
      <ProfileAvatar name={principal.display_name} />
      <h2>{principal.display_name}</h2>
      <dl className="profile-details">
        {Object.entries({ Role: roleLabels[principal.role], Email: principal.email || 'Not provided', Phone: principal.phone_number || 'Not provided' }).map(([label, value]) => <div key={label}><dt>{label}</dt><dd>{value}</dd></div>)}
      </dl>
      <p className="hint">Contact your hospital administrator to update your account details.</p>
    </section>
  </>;
}
