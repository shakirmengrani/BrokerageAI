const chatEndpoint = import.meta.env.VITE_CHAT_API_URL || "/api/chat";

export type ChatHistoryItem = {
  role: "user" | "assistant";
  content: string;
};

export type ChatPendingAction = {
  id: string;
  method: string;
  description: string;
  parameters: Record<string, unknown>;
  expiresAt: string;
};

export type ChatActionResult = {
  method: string;
  result: string;
};

export type IdentityAuthenticationResponse = {
  accessToken: string;
  tokenType: "Bearer";
  accessTokenExpiresAt: string;
  refreshToken: string;
  refreshTokenExpiresAt: string;
  user: {
    userId: string;
    email: string;
    roles: string[];
  };
};

type ChatResponse = {
  reply: string;
  pendingActions: ChatPendingAction[];
};

export type ChatProviderSettings = {
  endpoint: string | null;
  model: string | null;
  hasApiKey: boolean;
};

export type ChatFlow = {
  method: string;
  isReadOnly: boolean;
  description: string;
  parameters: string;
};

const identityStorageKeys = {
  user: "brokerage.identity.user",
  accessToken: "brokerage.identity.accessToken",
  accessTokenExpiresAt: "brokerage.identity.accessTokenExpiresAt",
  refreshToken: "brokerage.identity.refreshToken",
  refreshTokenExpiresAt: "brokerage.identity.refreshTokenExpiresAt",
} as const;

let identitySession: IdentityAuthenticationResponse | null = readStoredIdentitySession();
let identitySessionListener: ((session: IdentityAuthenticationResponse | null) => void) | null = null;
let refreshInProgress: Promise<boolean> | null = null;
let identitySessionVersion = 0;
const identityEndpoint = chatEndpoint.endsWith("/api/chat")
  ? `${chatEndpoint.slice(0, -"/api/chat".length)}/api/identity`
  : "/api/identity";
const chatSettingsEndpoint = chatEndpoint.endsWith("/api/chat")
  ? `${chatEndpoint.slice(0, -"/api/chat".length)}/api/admin/settings/chat`
  : "/api/admin/settings/chat";
const chatFlowsEndpoint = chatEndpoint.endsWith("/api/chat")
  ? `${chatEndpoint.slice(0, -"/api/chat".length)}/api/chat/flows`
  : "/api/chat/flows";

export function subscribeIdentitySession(
  listener: (session: IdentityAuthenticationResponse | null) => void,
): () => void {
  identitySessionListener = listener;
  listener(identitySession);
  const synchronizeFromStorage = (event: StorageEvent) => {
    if (event.key === null || Object.values(identityStorageKeys).some((key) => key === event.key)) {
      identitySession = readStoredIdentitySession();
      identitySessionVersion += 1;
      identitySessionListener?.(identitySession);
    }
  };
  window.addEventListener("storage", synchronizeFromStorage);
  return () => {
    window.removeEventListener("storage", synchronizeFromStorage);
    if (identitySessionListener === listener) identitySessionListener = null;
  };
}

function setIdentitySession(session: IdentityAuthenticationResponse | null): void {
  if (session) {
    persistIdentitySession(session);
  } else {
    identitySession = null;
    identitySessionVersion += 1;
    identitySessionListener?.(null);
    clearStoredIdentitySession();
    return;
  }
  identitySession = session;
  identitySessionVersion += 1;
  identitySessionListener?.(session);
}

function parseAuthenticationPayload(payload: unknown): IdentityAuthenticationResponse {
  if (!isRecord(payload) || typeof payload.accessToken !== "string" ||
      payload.tokenType !== "Bearer" || typeof payload.accessTokenExpiresAt !== "string" ||
      typeof payload.refreshToken !== "string" || typeof payload.refreshTokenExpiresAt !== "string" ||
      !isRecord(payload.user) || typeof payload.user.userId !== "string" ||
      typeof payload.user.email !== "string" || !Array.isArray(payload.user.roles) ||
      !payload.user.roles.every((role) => typeof role === "string")) {
    throw new Error("The identity API returned an invalid authentication response.");
  }
  return {
    accessToken: payload.accessToken,
    tokenType: "Bearer",
    accessTokenExpiresAt: payload.accessTokenExpiresAt,
    refreshToken: payload.refreshToken,
    refreshTokenExpiresAt: payload.refreshTokenExpiresAt,
    user: {
      userId: payload.user.userId,
      email: payload.user.email,
      roles: payload.user.roles,
    },
  };
}

async function parseAuthenticationResponse(response: Response): Promise<IdentityAuthenticationResponse> {
  return parseAuthenticationPayload(await response.json());
}

function readStoredIdentitySession(): IdentityAuthenticationResponse | null {
  if (typeof window === "undefined") return null;
  const accessToken = window.localStorage.getItem(identityStorageKeys.accessToken);
  const accessTokenExpiresAt = window.localStorage.getItem(identityStorageKeys.accessTokenExpiresAt);
  const refreshToken = window.localStorage.getItem(identityStorageKeys.refreshToken);
  const refreshTokenExpiresAt = window.localStorage.getItem(identityStorageKeys.refreshTokenExpiresAt);
  const storedUser = window.localStorage.getItem(identityStorageKeys.user);
  if (![accessToken, accessTokenExpiresAt, refreshToken, refreshTokenExpiresAt, storedUser].some(Boolean)) {
    return null;
  }
  if (!accessToken || !accessTokenExpiresAt || !refreshToken || !refreshTokenExpiresAt || !storedUser) {
    clearStoredIdentitySession();
    return null;
  }

  try {
    const user: unknown = JSON.parse(storedUser);
    const session = parseAuthenticationPayload({
      accessToken,
      tokenType: "Bearer",
      accessTokenExpiresAt,
      refreshToken,
      refreshTokenExpiresAt,
      user,
    });
    if (!Number.isFinite(Date.parse(accessTokenExpiresAt)) ||
        !Number.isFinite(Date.parse(refreshTokenExpiresAt)) ||
        Date.parse(refreshTokenExpiresAt) <= Date.now()) {
      clearStoredIdentitySession();
      return null;
    }
    return session;
  } catch {
    clearStoredIdentitySession();
    return null;
  }
}

function persistIdentitySession(session: IdentityAuthenticationResponse): void {
  try {
    window.localStorage.setItem(identityStorageKeys.user, JSON.stringify(session.user));
    window.localStorage.setItem(identityStorageKeys.accessToken, session.accessToken);
    window.localStorage.setItem(identityStorageKeys.accessTokenExpiresAt, session.accessTokenExpiresAt);
    window.localStorage.setItem(identityStorageKeys.refreshToken, session.refreshToken);
    window.localStorage.setItem(identityStorageKeys.refreshTokenExpiresAt, session.refreshTokenExpiresAt);
  } catch (error) {
    clearStoredIdentitySession();
    throw new Error("Could not save your sign-in session in this browser.", { cause: error });
  }
}

function clearStoredIdentitySession(): void {
  for (const key of Object.values(identityStorageKeys)) {
    window.localStorage.removeItem(key);
  }
}

async function requestAuthentication(
  route: "register" | "login",
  email: string,
  password: string,
): Promise<IdentityAuthenticationResponse> {
  const response = await fetch(`${identityEndpoint}/${route}`, {
    method: "POST",
    headers: { "Content-Type": "application/json", Accept: "application/json" },
    body: JSON.stringify({ email, password }),
  });
  if (!response.ok) {
    if (response.status === 409) throw new Error("An account with this email already exists.");
    if (response.status === 401) throw new Error("The email or password is invalid.");
    if (response.status === 429) throw new Error("Too many attempts. Wait a minute and try again.");
    if (response.status === 400) {
      const problem: unknown = await response.json();
      if (isRecord(problem) && typeof problem.detail === "string") throw new Error(problem.detail);
      throw new Error("Check your email and password and try again.");
    }
    throw new Error(`The identity service returned HTTP ${response.status}.`);
  }
  const session = await parseAuthenticationResponse(response);
  setIdentitySession(session);
  return session;
}

export function registerIdentity(
  email: string,
  password: string,
): Promise<IdentityAuthenticationResponse> {
  return requestAuthentication("register", email, password);
}

export function loginIdentity(
  email: string,
  password: string,
): Promise<IdentityAuthenticationResponse> {
  return requestAuthentication("login", email, password);
}

async function refreshIdentitySession(): Promise<boolean> {
  if (!identitySession) return false;
  if (refreshInProgress) return refreshInProgress;
  const sessionBeingRefreshed = identitySession;
  const versionBeingRefreshed = identitySessionVersion;
  refreshInProgress = (async () => {
    try {
      let response: Response;
      try {
        response = await fetch(`${identityEndpoint}/refresh`, {
          method: "POST",
          headers: { "Content-Type": "application/json", Accept: "application/json" },
          body: JSON.stringify({ refreshToken: sessionBeingRefreshed.refreshToken }),
        });
      } catch (error) {
        throw new Error("Could not reach the identity service to refresh your session.", { cause: error });
      }

      if (response.status === 401) {
        if (identitySessionVersion === versionBeingRefreshed) {
          setIdentitySession(null);
          return false;
        }
        return identitySession !== null;
      }
      if (!response.ok) {
        throw new Error(`The identity service could not refresh the session (HTTP ${response.status}).`);
      }

      const refreshedSession = await parseAuthenticationResponse(response);
      if (identitySessionVersion !== versionBeingRefreshed) return identitySession !== null;
      setIdentitySession(refreshedSession);
      return true;
    } finally {
      refreshInProgress = null;
    }
  })();
  return refreshInProgress;
}

async function authenticatedFetch(url: string, init: RequestInit = {}): Promise<Response> {
  if (!identitySession) throw new Error("Sign in to use the brokerage assistant.");
  if (Date.parse(identitySession.accessTokenExpiresAt) <= Date.now() + 30_000 &&
      !await refreshIdentitySession()) {
    throw new Error("Your session expired. Sign in again.");
  }

  const send = () => fetch(url, {
    ...init,
    headers: {
      ...Object.fromEntries(new Headers(init.headers).entries()),
      Authorization: `Bearer ${identitySession!.accessToken}`,
    },
  });
  let response = await send();
  if (response.status === 401 && await refreshIdentitySession()) {
    response = await send();
  }
  if (response.status === 401) setIdentitySession(null);
  return response;
}

export async function logoutIdentity(): Promise<void> {
  const pendingRefresh = refreshInProgress;
  if (pendingRefresh) {
    try {
      await pendingRefresh;
    } catch {
      // Continue logout with the current token even if rotation could not complete.
    }
  }
  const session = identitySession;
  setIdentitySession(null);
  const refreshToken = session?.refreshToken;
  if (!refreshToken) {
    return;
  }
  try {
    const response = await fetch(`${identityEndpoint}/logout`, {
      method: "POST",
      headers: { "Content-Type": "application/json", Accept: "application/json" },
      body: JSON.stringify({ refreshToken }),
    });
    if (!response.ok) throw new Error(`Sign out failed with HTTP ${response.status}.`);
  } catch (error) {
    throw new Error(
      "You were signed out on this browser, but the server could not revoke the refresh token. Sign out may remain active until it expires.",
      { cause: error },
    );
  }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function parseChatProviderSettings(payload: unknown): ChatProviderSettings {
  if (!isRecord(payload) ||
      !(typeof payload.endpoint === "string" || payload.endpoint === null) ||
      !(typeof payload.model === "string" || payload.model === null) ||
      typeof payload.hasApiKey !== "boolean") {
    throw new Error("The settings API returned an invalid response.");
  }
  return {
    endpoint: payload.endpoint,
    model: payload.model,
    hasApiKey: payload.hasApiKey,
  };
}

async function throwSettingsRequestError(response: Response): Promise<never> {
  if (response.status === 401) throw new Error("Sign in to manage chat settings.");
  if (response.status === 403) throw new Error("Only an Administrator can manage chat settings.");
  const problem: unknown = await response.json().catch(() => undefined);
  if (isRecord(problem) && typeof problem.detail === "string")
    throw new Error(problem.detail);
  throw new Error(`The settings service returned HTTP ${response.status}.`);
}

export async function getChatProviderSettings(): Promise<ChatProviderSettings> {
  const response = await authenticatedFetch(chatSettingsEndpoint, {
    headers: { Accept: "application/json" },
  });
  if (!response.ok) await throwSettingsRequestError(response);
  return parseChatProviderSettings(await response.json());
}

export async function saveChatProviderSettings(
  settings: { endpoint: string; model: string; apiKey: string },
): Promise<ChatProviderSettings> {
  const response = await authenticatedFetch(chatSettingsEndpoint, {
    method: "PUT",
    headers: { "Content-Type": "application/json", Accept: "application/json" },
    body: JSON.stringify({
      endpoint: settings.endpoint,
      model: settings.model,
      apiKey: settings.apiKey || undefined,
    }),
  });
  if (!response.ok) await throwSettingsRequestError(response);
  return parseChatProviderSettings(await response.json());
}

export async function getChatFlows(): Promise<ChatFlow[]> {
  const response = await authenticatedFetch(chatFlowsEndpoint, {
    headers: { Accept: "application/json" },
  });
  if (!response.ok) {
    if (response.status === 401) throw new Error("Sign in to view available chat flows.");
    throw new Error(`The chat flow catalog returned HTTP ${response.status}.`);
  }

  const payload: unknown = await response.json();
  if (!Array.isArray(payload)) throw new Error("The chat flow catalog returned an invalid response.");
  return payload.map((flow): ChatFlow => {
    if (!isRecord(flow) ||
        typeof flow.method !== "string" ||
        typeof flow.isReadOnly !== "boolean" ||
        typeof flow.description !== "string" ||
        typeof flow.parameters !== "string") {
      throw new Error("The chat flow catalog returned an invalid flow.");
    }
    return {
      method: flow.method,
      isReadOnly: flow.isReadOnly,
      description: flow.description,
      parameters: flow.parameters,
    };
  });
}

export async function sendChatMessage(
  message: string,
  history: ChatHistoryItem[],
  signal: AbortSignal,
): Promise<ChatResponse> {
  const response = await authenticatedFetch(chatEndpoint, {
    method: "POST",
    headers: { "Content-Type": "application/json", Accept: "application/json" },
    body: JSON.stringify({ message, history }),
    signal,
  });

  if (!response.ok) {
    if (response.status === 401) {
      throw new Error("Sign in to use the brokerage assistant.");
    }
    if (response.status === 503) {
      throw new Error("Chat is unavailable. The Host needs Chat:Endpoint and Chat:Model configuration.");
    }
    if (response.status === 502) {
      throw new Error("The chat provider could not complete the request. Please try again.");
    }
    throw new Error(`The chat service returned HTTP ${response.status}.`);
  }

  const payload: unknown = await response.json();
  if (!isRecord(payload) || typeof payload.reply !== "string" ||
      !Array.isArray(payload.pendingActions)) {
    throw new Error("The chat API response is missing its reply or pending actions.");
  }

  const pendingActions: ChatPendingAction[] = payload.pendingActions.map((action): ChatPendingAction => {
    if (!isRecord(action) || typeof action.id !== "string" ||
        typeof action.method !== "string" || typeof action.description !== "string" ||
        typeof action.expiresAt !== "string" || !isRecord(action.parameters)) {
      throw new Error("The chat API returned an invalid pending action.");
    }
    return {
      id: action.id,
      method: action.method,
      description: action.description,
      expiresAt: action.expiresAt,
      parameters: action.parameters,
    };
  });
  return { reply: payload.reply, pendingActions };
}

export async function confirmChatAction(actionId: string): Promise<ChatActionResult> {
  const response = await authenticatedFetch(`${chatEndpoint}/antiforgery`, {
    headers: { Accept: "application/json" },
  });
  if (!response.ok) throw new Error(`The confirmation token service returned HTTP ${response.status}.`);
  const tokenPayload: unknown = await response.json();
  if (!isRecord(tokenPayload) || typeof tokenPayload.token !== "string") {
    throw new Error("The confirmation token service returned an invalid response.");
  }

  const confirmResponse = await authenticatedFetch(`${chatEndpoint}/actions/${encodeURIComponent(actionId)}/confirm`, {
    method: "POST",
    headers: { Accept: "application/json", "X-CSRF-TOKEN": tokenPayload.token },
  });

  if (!confirmResponse.ok) {
    if (confirmResponse.status === 401) throw new Error("Sign in to confirm this action.");
    if (confirmResponse.status === 403) throw new Error("You are no longer authorized to perform this action.");
    if (confirmResponse.status === 404) throw new Error("This action draft expired or is no longer available.");
    if (confirmResponse.status === 409) throw new Error("This action could not be completed. Create a new draft.");
    if (confirmResponse.status === 400) throw new Error("The confirmation request expired. Please try again.");
    throw new Error(`The action service returned HTTP ${confirmResponse.status}.`);
  }

  const result: unknown = await confirmResponse.json();
  if (!isRecord(result) || typeof result.method !== "string" || typeof result.result !== "string") {
    throw new Error("The action API returned an invalid response.");
  }

  return { method: result.method, result: result.result };
}

export async function cancelChatAction(actionId: string): Promise<void> {
  const response = await authenticatedFetch(`${chatEndpoint}/actions/${encodeURIComponent(actionId)}`, {
    method: "DELETE",
    headers: { Accept: "application/json" },
  });
  if (!response.ok && response.status !== 404) {
    throw new Error(`The action service returned HTTP ${response.status}.`);
  }
}
