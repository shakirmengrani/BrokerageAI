import { useEffect, useRef, useState, type FormEvent, type KeyboardEvent } from "react";
import {
  cancelChatAction,
  confirmChatAction,
  getChatFlows,
  getChatProviderSettings,
  loginIdentity,
  logoutIdentity,
  registerIdentity,
  saveChatProviderSettings,
  sendChatMessage,
  subscribeIdentitySession,
  type ChatProviderSettings,
  type ChatHistoryItem,
  type ChatFlow,
  type ChatPendingAction,
  type IdentityAuthenticationResponse,
} from "./chatApi";

type ActionState = ChatPendingAction & {
  status: "pending" | "confirming" | "canceling" | "completed" | "canceled" | "failed";
  outcome?: string;
};

type Message = {
  id: number;
  role: "user" | "assistant" | "error";
  content: string;
  pendingActions?: ActionState[];
};

const suggestions = [
  { label: "Account balance", prompt: "Show my account balance" },
  { label: "Latest market quote", prompt: "Get the latest market quote" },
  { label: "Help", prompt: "What can you help me with?" },
];

function ChatFlowsSidebar({
  isOpen,
  onClose,
  flows,
  error,
}: {
  isOpen: boolean;
  onClose: () => void;
  flows: ChatFlow[];
  error: string;
}) {
  const [filter, setFilter] = useState("");
  const normalizedFilter = filter.trim().toLowerCase();
  const filteredFlows = flows.filter((flow) =>
    `${flow.method} ${flow.description} ${flow.parameters}`.toLowerCase().includes(normalizedFilter));
  const groupedFlows = filteredFlows.reduce<Map<string, ChatFlow[]>>((groups, flow) => {
    const category = flow.method.split(".")[0];
    const categoryFlows = groups.get(category) ?? [];
    categoryFlows.push(flow);
    groups.set(category, categoryFlows);
    return groups;
  }, new Map());

  return (
    <aside
      className={`flow-sidebar ${isOpen ? "flow-sidebar-open" : "flow-sidebar-collapsed"}`}
      aria-label="Chat flow catalog"
      aria-hidden={!isOpen}
      inert={!isOpen}
      id="chat-flow-catalog"
    >
      <header className="flow-sidebar-header">
        <div>
          <p className="flow-sidebar-eyebrow">ASSISTANT TOOLS</p>
          <h2>Flow catalog <span>{flows.length}</span></h2>
        </div>
        <button className="icon-button flow-sidebar-close" aria-label="Close flow catalog" onClick={onClose}>
          ×
        </button>
      </header>
      <label className="flow-search">
        <span aria-hidden="true">⌕</span>
        <input
          aria-label="Filter chat flows"
          onChange={(event) => setFilter(event.target.value)}
          placeholder="Filter flows..."
          type="search"
          value={filter}
        />
      </label>
      <div className="flow-access-legend">
        <span><i className="flow-legend-read" /> Read-only</span>
        <span><i className="flow-legend-confirm" /> Confirmation required</span>
      </div>
      {error ? (
        <p className="flow-catalog-message" role="alert">{error}</p>
      ) : flows.length === 0 ? (
        <p className="flow-catalog-message">Loading catalog…</p>
      ) : filteredFlows.length === 0 ? (
        <p className="flow-catalog-message">No matching flows.</p>
      ) : (
        <div className="flow-catalog-list">
          {[...groupedFlows.entries()].map(([category, categoryFlows]) => (
            <section className="flow-category" key={category}>
              <h3>{category}</h3>
              {categoryFlows.map((flow) => (
                <article className="flow-card" key={flow.method}>
                  <div className="flow-card-heading">
                    <code>{flow.method}</code>
                    <span
                      className={`flow-mode ${flow.isReadOnly ? "flow-mode-read" : "flow-mode-confirm"}`}
                      title={flow.isReadOnly ? "Executes as a read-only operation" : "Requires explicit confirmation"}
                    >
                      {flow.isReadOnly ? "READ" : "CONFIRM"}
                    </span>
                  </div>
                  <p>{flow.description}</p>
                  <span className="flow-parameters">{flow.parameters}</span>
                </article>
              ))}
            </section>
          ))}
        </div>
      )}
      <footer className="flow-sidebar-footer">
        State-changing flows are prepared as drafts and run only after you confirm.
      </footer>
    </aside>
  );
}

function IdentityScreen({ notice }: { notice: string }) {
  const [mode, setMode] = useState<"login" | "register">("login");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function submitIdentity(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsSubmitting(true);
    setError("");
    try {
      if (mode === "register") await registerIdentity(email, password);
      else await loginIdentity(email, password);
    } catch (submissionError) {
      setError(submissionError instanceof Error ? submissionError.message : "The identity request failed.");
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <main className="identity-shell">
      <section className="identity-card">
        <a className="brand identity-brand" href="/" aria-label="Brokerage Terminal home">
          <span className="brand-mark" aria-hidden="true">B</span>
          <span>brokerage<span className="brand-dot">.</span></span>
        </a>
        <p className="identity-eyebrow">SECURE BROKERAGE ACCESS</p>
        <h1>{mode === "login" ? "Welcome back." : "Create your account."}</h1>
        <p className="identity-copy">
          {mode === "login"
            ? "Sign in to continue to your brokerage workspace."
            : "Register with your email and a password of at least 12 characters."}
        </p>
        <form className="identity-form" onSubmit={(event) => void submitIdentity(event)}>
          <label htmlFor="identity-email">Email</label>
          <input
            autoComplete="email"
            id="identity-email"
            maxLength={320}
            onChange={(event) => setEmail(event.target.value)}
            required
            type="email"
            value={email}
          />
          <label htmlFor="identity-password">Password</label>
          <input
            autoComplete={mode === "login" ? "current-password" : "new-password"}
            id="identity-password"
            maxLength={256}
            minLength={mode === "register" ? 12 : 1}
            onChange={(event) => setPassword(event.target.value)}
            required
            type="password"
            value={password}
          />
          {error && <p className="identity-error" role="alert">{error}</p>}
          {notice && <p className="identity-error" role="alert">{notice}</p>}
          <button disabled={isSubmitting} type="submit">
            {isSubmitting ? "Please wait…" : mode === "login" ? "Sign in" : "Create account"}
          </button>
        </form>
        <p className="identity-switch">
          {mode === "login" ? "New to this workspace?" : "Already have an account?"}{" "}
          <button
            onClick={() => {
              setMode(mode === "login" ? "register" : "login");
              setError("");
            }}
            type="button"
          >
            {mode === "login" ? "Create an account" : "Sign in"}
          </button>
        </p>
        <p className="identity-security-note">
          Your user profile and authentication tokens are saved in this browser until you sign out.
        </p>
      </section>
    </main>
  );
}

function ChatSettingsView() {
  const [settings, setSettings] = useState<ChatProviderSettings | null>(null);
  const [endpoint, setEndpoint] = useState("");
  const [model, setModel] = useState("");
  const [apiKey, setApiKey] = useState("");
  const [error, setError] = useState("");
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [saved, setSaved] = useState(false);

  useEffect(() => {
    let active = true;
    void getChatProviderSettings()
      .then((current) => {
        if (!active) return;
        setSettings(current);
        setEndpoint(current.endpoint ?? "");
        setModel(current.model ?? "");
      })
      .catch((loadError: unknown) => {
        if (active)
          setError(loadError instanceof Error ? loadError.message : "Could not load chat settings.");
      })
      .finally(() => {
        if (active) setIsLoading(false);
      });
    return () => { active = false; };
  }, []);

  async function submitSettings(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsSaving(true);
    setError("");
    setSaved(false);
    try {
      const updated = await saveChatProviderSettings({ endpoint, model, apiKey });
      setSettings(updated);
      setEndpoint(updated.endpoint ?? "");
      setModel(updated.model ?? "");
      setApiKey("");
      setSaved(true);
    } catch (saveError) {
      setError(saveError instanceof Error ? saveError.message : "Could not save chat settings.");
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <section className="settings-page">
      <div className="settings-panel">
        <p className="settings-eyebrow">ADMINISTRATION / PROVIDER</p>
        <h1>Chat settings</h1>
        <p className="settings-description">
          Configure the OpenAI-compatible provider used by the assistant. Updates take effect immediately.
        </p>
        {isLoading ? (
          <p className="settings-status" role="status">Loading current settings…</p>
        ) : (
          <form className="settings-form" onSubmit={(event) => void submitSettings(event)}>
            <label htmlFor="chat-provider-endpoint">Chat completions URL</label>
            <input
              autoComplete="url"
              id="chat-provider-endpoint"
              maxLength={2048}
              onChange={(event) => setEndpoint(event.target.value)}
              placeholder="https://api.openai.com/v1/chat/completions"
              required
              type="url"
              value={endpoint}
            />
            <label htmlFor="chat-provider-model">Model</label>
            <input
              autoComplete="off"
              id="chat-provider-model"
              maxLength={200}
              onChange={(event) => setModel(event.target.value)}
              placeholder="Model identifier provided by your provider"
              required
              value={model}
            />
            <label htmlFor="chat-provider-api-key">API key</label>
            <input
              autoComplete="new-password"
              id="chat-provider-api-key"
              maxLength={4096}
              onChange={(event) => setApiKey(event.target.value)}
              placeholder={settings?.hasApiKey ? "Saved securely; enter to replace" : "Enter provider API key"}
              type="password"
              value={apiKey}
            />
            <p className="settings-security-note">
              {settings?.hasApiKey
                ? "A provider key is configured. Leave the field blank to keep it unchanged."
                : "Provider keys are encrypted before being saved and are never returned to the browser."}
              {" "}Only an Administrator can change these settings.
            </p>
            {error && <p className="identity-error" role="alert">{error}</p>}
            {saved && <p className="settings-success" role="status">Settings saved. They are active now.</p>}
            <button disabled={isSaving} type="submit">
              {isSaving ? "Saving…" : "Save settings"}
            </button>
          </form>
        )}
      </div>
    </section>
  );
}

function App() {
  const [identitySession, setIdentitySession] = useState<IdentityAuthenticationResponse | null>(null);
  const [logoutNotice, setLogoutNotice] = useState("");
  const [activeView, setActiveView] = useState<"assistant" | "settings">("assistant");
  const [flows, setFlows] = useState<ChatFlow[]>([]);
  const [flowsError, setFlowsError] = useState("");
  const [messages, setMessages] = useState<Message[]>([]);
  const [draft, setDraft] = useState("");
  const [isSending, setIsSending] = useState(false);
  const [isSidebarOpen, setIsSidebarOpen] = useState(false);
  const [isFlowsOpen, setIsFlowsOpen] = useState(false);
  const nextId = useRef(0);
  const textareaRef = useRef<HTMLTextAreaElement>(null);
  const messageEndRef = useRef<HTMLDivElement>(null);
  const requestController = useRef<AbortController | null>(null);

  useEffect(() => subscribeIdentitySession(setIdentitySession), []);

  useEffect(() => {
    if (identitySession) setLogoutNotice("");
  }, [identitySession?.user.userId]);

  useEffect(() => {
    if (!identitySession) {
      setFlows([]);
      setFlowsError("");
      return;
    }

    let active = true;
    setFlowsError("");
    void getChatFlows()
      .then((catalog) => {
        if (active) setFlows(catalog);
      })
      .catch((error: unknown) => {
        if (active) setFlowsError(error instanceof Error ? error.message : "Could not load chat flows.");
      });
    return () => { active = false; };
  }, [identitySession?.user.userId]);

  useEffect(() => {
    messageEndRef.current?.scrollIntoView({ behavior: "smooth", block: "end" });
  }, [messages, isSending]);

  if (!identitySession) return <IdentityScreen notice={logoutNotice} />;

  async function submitMessage(text: string) {
    const trimmed = text.trim();
    if (!trimmed || isSending) return;

    setMessages((current) => [...current, { id: nextId.current++, role: "user", content: trimmed }]);
    setDraft("");
    setIsSending(true);
    if (textareaRef.current) textareaRef.current.style.height = "auto";

    const controller = new AbortController();
    requestController.current = controller;
    try {
      const history: ChatHistoryItem[] = messages
        .filter((item): item is Message & { role: "user" | "assistant" } =>
          item.role === "user" || item.role === "assistant")
        .slice(-20)
        .map(({ role, content }) => ({ role, content }));
      const response = await sendChatMessage(trimmed, history, controller.signal);
      setMessages((current) => [
        ...current,
        {
          id: nextId.current++,
          role: "assistant",
          content: response.reply,
          pendingActions: response.pendingActions.map((action) => ({ ...action, status: "pending" })),
        },
      ]);
    } catch (error) {
      if (!controller.signal.aborted) {
        setMessages((current) => [
          ...current,
          {
            id: nextId.current++,
            role: "error",
            content: error instanceof Error ? error.message : "The chat request failed.",
          },
        ]);
      }
    } finally {
      if (requestController.current === controller) {
        requestController.current = null;
        setIsSending(false);
        textareaRef.current?.focus();
      }
    }
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    void submitMessage(draft);
  }

  function handleComposerKeyDown(event: KeyboardEvent<HTMLTextAreaElement>) {
    if (event.key === "Enter" && !event.shiftKey) {
      event.preventDefault();
      void submitMessage(draft);
    }
  }

  function updateAction(
    messageId: number,
    actionId: string,
    changes: Partial<ActionState>,
  ) {
    setMessages((current) => current.map((message) =>
      message.id !== messageId || !message.pendingActions
        ? message
        : {
            ...message,
            pendingActions: message.pendingActions.map((action) =>
              action.id === actionId ? { ...action, ...changes } : action),
          }));
  }

  async function confirmAction(messageId: number, action: ActionState) {
    updateAction(messageId, action.id, { status: "confirming", outcome: undefined });
    try {
      const result = await confirmChatAction(action.id);
      updateAction(messageId, action.id, {
        status: "completed",
        outcome: `Completed ${result.method}:\n${result.result}`,
      });
    } catch (error) {
      updateAction(messageId, action.id, {
        status: "failed",
        outcome: error instanceof Error ? error.message : "The action could not be completed.",
      });
    }
  }

  async function cancelAction(messageId: number, action: ActionState) {
    updateAction(messageId, action.id, { status: "canceling" });
    try {
      await cancelChatAction(action.id);
      updateAction(messageId, action.id, { status: "canceled", outcome: "Draft discarded." });
    } catch (error) {
      updateAction(messageId, action.id, {
        status: "failed",
        outcome: error instanceof Error ? error.message : "The draft could not be discarded.",
      });
    }
  }

  function startNewChat() {
    requestController.current?.abort();
    requestController.current = null;
    setIsSending(false);
    setMessages([]);
    setDraft("");
    setIsSidebarOpen(false);
    textareaRef.current?.focus();
  }

  return (
    <div className="app-shell">
      {isSidebarOpen && (
        <button
          aria-label="Close navigation"
          className="sidebar-scrim"
          onClick={() => setIsSidebarOpen(false)}
        />
      )}
      {isFlowsOpen && (
        <button
          aria-label="Close flow catalog"
          className="flow-sidebar-scrim"
          onClick={() => setIsFlowsOpen(false)}
        />
      )}
      <aside className={`sidebar ${isSidebarOpen ? "sidebar-open" : ""}`}>
        <div className="sidebar-top">
          <a className="brand" href="/" aria-label="Brokerage Terminal home">
            <span className="brand-mark" aria-hidden="true">B</span>
            <span>brokerage<span className="brand-dot">.</span></span>
          </a>
          <button
            className="icon-button sidebar-close"
            aria-label="Close navigation"
            onClick={() => setIsSidebarOpen(false)}
          >
            ×
          </button>
        </div>

        <button className="new-chat-button" onClick={startNewChat}>
          <span aria-hidden="true">＋</span>
          <span>New conversation</span>
          <kbd>⌘ K</kbd>
        </button>

        <div className="sidebar-section">
          <p className="section-label">WORKSPACE</p>
          <button
            className={`nav-item ${activeView === "assistant" ? "nav-item-active" : ""}`}
            onClick={() => setActiveView("assistant")}
            type="button"
          >
            <span className="nav-icon" aria-hidden="true">›_</span>
            <span>Assistant</span>
          </button>
          {identitySession.user.roles.includes("Administrator") && (
            <button
              className={`nav-item ${activeView === "settings" ? "nav-item-active" : ""}`}
              onClick={() => setActiveView("settings")}
              type="button"
            >
              <span className="nav-icon nav-icon-muted" aria-hidden="true">⚙</span>
              <span>Settings</span>
            </button>
          )}
          <button className="nav-item" title="Conversation history is not persisted yet">
            <span className="nav-icon nav-icon-muted" aria-hidden="true">◷</span>
            <span>History</span>
            <span className="nav-soon">soon</span>
          </button>
        </div>

        <div className="sidebar-bottom">
          <div className="environment-card">
            <span className="environment-dot" />
            <div>
              <p className="environment-title">Development</p>
              <p className="environment-subtitle">Brokerage workspace</p>
            </div>
            <span className="environment-tag">LOCAL</span>
          </div>
          <div className="profile-row">
            <div className="avatar">BT</div>
            <div className="profile-copy">
              <span>{identitySession.user.email}</span>
              <span className="profile-note">
                {identitySession.user.roles.length ? identitySession.user.roles.join(", ") : "No assigned roles"}
              </span>
            </div>
            <button
              className="profile-menu"
              onClick={() => {
                void logoutIdentity().then(
                  () => setLogoutNotice(""),
                  (error: unknown) => setLogoutNotice(
                    error instanceof Error ? error.message : "Sign out failed.",
                  ),
                );
              }}
              type="button"
            >
              Sign out
            </button>
          </div>
        </div>
      </aside>

      <main className="main-panel">
        <header className="topbar">
          <div className="topbar-left">
            <button
              className="icon-button menu-button"
              aria-label="Open navigation"
              onClick={() => setIsSidebarOpen(true)}
            >
              ☰
            </button>
            <span className="breadcrumb-muted">Workspace</span>
            <span className="breadcrumb-divider">/</span>
            <span className="breadcrumb-current">{activeView === "assistant" ? "Assistant" : "Settings"}</span>
          </div>
          <div className="topbar-right">
            <span className="connection-badge">
              <span className="connection-dot" />
              Chat API
            </span>
            {activeView === "assistant" && (
              <button
                aria-expanded={isFlowsOpen}
                aria-controls="chat-flow-catalog"
                aria-label={isFlowsOpen ? "Close chat flow catalog" : "Open chat flow catalog"}
                className="flow-toggle"
                onClick={() => setIsFlowsOpen((open) => !open)}
                type="button"
              >
                <span aria-hidden="true">{isFlowsOpen ? "×" : "☷"}</span>
                <span>{isFlowsOpen ? "Hide flows" : "Flows"}</span>
              </button>
            )}
            <button
              className="help-button"
              aria-label="Chat provider settings"
              onClick={() => {
                if (identitySession.user.roles.includes("Administrator"))
                  setActiveView("settings");
              }}
              title="Chat provider settings are managed by an Administrator"
            >
              ?
            </button>
          </div>
        </header>

        <div className="workspace-content">
        {activeView === "settings" ? <ChatSettingsView /> : <div className="chat-column">
        <section className={`conversation ${messages.length ? "conversation-active" : ""}`}>
          {messages.length === 0 ? (
            <div className="welcome">
              <div className="welcome-eyebrow">
                <span className="welcome-prompt">~/brokerage</span>
                <span className="eyebrow-divider">/</span>
                <span>ASSISTANT WORKSPACE</span>
              </div>
              <div className="welcome-mark" aria-hidden="true">
                <span className="welcome-mark-cursor">_</span>
              </div>
              <h1>Markets move.<br /><span>Get clarity.</span></h1>
              <p className="welcome-copy">
                A focused workspace for brokerage insights.<br />
                Ask a question to get started.
              </p>

              <div className="suggestion-grid" aria-label="Suggested prompts">
                {suggestions.map((suggestion, index) => (
                  <button
                    className="suggestion-card"
                    key={suggestion.label}
                    onClick={() => void submitMessage(suggestion.prompt)}
                    disabled={isSending}
                  >
                    <span className={`suggestion-icon suggestion-icon-${index}`} aria-hidden="true">
                      {["◫", "⌁", "?"][index]}
                    </span>
                    <span>{suggestion.label}</span>
                    <span className="suggestion-arrow" aria-hidden="true">↗</span>
                  </button>
                ))}
              </div>
              <div className="integration-note">
                <span className="note-icon" aria-hidden="true">i</span>
                <span>
                  Module workflows · Changes remain drafts until you confirm them.
                </span>
              </div>
            </div>
          ) : (
            <div className="message-list" aria-live="polite" aria-relevant="additions text">
              {messages.map((message) => (
                <article className={`message message-${message.role}`} key={message.id}>
                  <div className={`message-avatar message-avatar-${message.role}`}>
                    {message.role === "user" ? "U" : message.role === "error" ? "!" : "B"}
                  </div>
                  <div className="message-body">
                    <div className="message-heading">
                      <span>{message.role === "user" ? "You" : message.role === "error" ? "Connection" : "Brokerage assistant"}</span>
                      {message.role === "error" && <span className="error-label">REQUEST FAILED</span>}
                    </div>
                    <p>{message.content}</p>
                    {message.pendingActions?.map((action) => (
                      <section className="action-draft" key={action.id} aria-label="Pending action draft">
                        <div className="action-draft-heading">
                          <span>CONFIRMATION REQUIRED</span>
                          <code>{action.method}</code>
                        </div>
                        <p>{action.description}</p>
                        <pre>{JSON.stringify(action.parameters, null, 2)}</pre>
                        <p className="action-expiry">
                          Expires {new Date(action.expiresAt).toLocaleTimeString()}
                        </p>
                        {action.outcome && <pre className="action-outcome">{action.outcome}</pre>}
                        {action.status === "pending" && (
                          <div className="action-buttons">
                            <button onClick={() => void confirmAction(message.id, action)}>
                              Confirm and execute
                            </button>
                            <button className="action-cancel" onClick={() => void cancelAction(message.id, action)}>
                              Discard
                            </button>
                          </div>
                        )}
                        {(action.status === "confirming" || action.status === "canceling") && (
                          <p role="status">Processing action…</p>
                        )}
                        {(action.status === "completed" || action.status === "canceled") && (
                          <p className="action-complete" role="status">{action.status === "completed" ? "Completed" : "Discarded"}</p>
                        )}
                      </section>
                    ))}
                  </div>
                </article>
              ))}
              {isSending && (
                <div className="message message-assistant" role="status">
                  <div className="message-avatar message-avatar-assistant">B</div>
                  <div className="message-body">
                    <div className="message-heading"><span>Brokerage assistant</span></div>
                    <p className="typing-indicator"><span /><span /><span /></p>
                  </div>
                </div>
              )}
              <div ref={messageEndRef} />
            </div>
          )}
        </section>

        <div className="composer-wrap">
          <form className="composer" onSubmit={handleSubmit}>
            <span className="composer-prompt" aria-hidden="true">$</span>
            <textarea
              aria-label="Message the brokerage assistant"
              autoComplete="off"
              maxLength={4000}
              onChange={(event) => {
                setDraft(event.target.value);
                event.currentTarget.style.height = "auto";
                event.currentTarget.style.height = `${Math.min(event.currentTarget.scrollHeight, 180)}px`;
              }}
              onKeyDown={handleComposerKeyDown}
              placeholder="Ask about markets, accounts, and more..."
              ref={textareaRef}
              rows={1}
              value={draft}
            />
            <button
              aria-label="Send message"
              className="send-button"
              disabled={!draft.trim() || isSending}
              type="submit"
            >
              ↑
            </button>
          </form>
          <div className="composer-meta">
            <span><kbd>↵</kbd> send <span className="meta-divider">·</span> <kbd>⇧ ↵</kbd> new line</span>
            <span>{draft.length.toLocaleString()} / 4,000</span>
          </div>
          <p className="disclaimer">
            For informational use only. Verify important details before acting.
          </p>
        </div>
        </div>}
        {activeView === "assistant" && (
          <ChatFlowsSidebar
            error={flowsError}
            flows={flows}
            isOpen={isFlowsOpen}
            onClose={() => setIsFlowsOpen(false)}
          />
        )}
        </div>
      </main>
    </div>
  );
}

export default App;
