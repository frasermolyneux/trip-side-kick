import { createContext, useContext } from 'react';

import type { AuthMeResponse } from '../api/auth';
import type { ClientConfig } from '../api/clientConfig';

export interface AuthContextValue {
  auth: AuthMeResponse;
  config: ClientConfig | undefined;
}

export const AuthContext = createContext<AuthContextValue | undefined>(undefined);

/** The signed-in user's session state and client config, loaded once in `App` and shared via context. */
export function useAuth(): AuthContextValue {
  const value = useContext(AuthContext);

  if (!value) {
    throw new Error('useAuth must be used within an AuthContext.Provider.');
  }

  return value;
}
