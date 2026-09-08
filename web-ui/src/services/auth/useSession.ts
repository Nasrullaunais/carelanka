import { useSyncExternalStore } from 'react';
import { getSession, subscribe } from './session';

// useSyncExternalStore rather than a context + useEffect: the session is written from the
// transport interceptor on a 401, which is outside React entirely. This is what makes that
// write reach the UI.
export function useSession() {
  return useSyncExternalStore(subscribe, getSession, getSession);
}
