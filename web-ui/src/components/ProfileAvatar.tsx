export function ProfileAvatar({ name }: { name: string }) {
  const initials = name.trim().split(/\s+/).filter(Boolean).slice(0, 2).map((part) => part[0]).join('').toUpperCase();
  return <span className="profile-avatar" aria-hidden="true">{initials || '?'}</span>;
}
