import { useEffect, useRef, useState, type ChangeEvent, type FormEvent, type KeyboardEvent } from "react";
import { createPortal } from "react-dom";
import {
  cancelChatAction,
  callBrokerageFlow,
  confirmChatAction,
  getBrokerageFlows,
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
  type WorkflowParameter,
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
  const [selectedFlow, setSelectedFlow] = useState<ChatFlow | null>(null);
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

  useEffect(() => {
    if (!isOpen) setSelectedFlow(null);
  }, [isOpen]);

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
                  <button
                    className="flow-form-open"
                    onClick={() => setSelectedFlow(flow)}
                    type="button"
                  >
                    Open form <span aria-hidden="true">→</span>
                  </button>
                </article>
              ))}
            </section>
          ))}
        </div>
      )}
      <footer className="flow-sidebar-footer">
        State-changing flows require explicit confirmation before they run.
      </footer>
      {selectedFlow && createPortal(
        <div
          className="workflow-dialog-backdrop"
          onKeyDown={(event) => {
            if (event.key === "Escape") setSelectedFlow(null);
          }}
          onMouseDown={(event) => {
            if (event.target === event.currentTarget) setSelectedFlow(null);
          }}
          tabIndex={-1}
        >
          <section
            aria-labelledby="catalog-workflow-title"
            aria-modal="true"
            className="workflow-dialog"
            role="dialog"
          >
            <header className="workflow-dialog-heading">
              <div>
                <p className="dashboard-eyebrow">FLOW CATALOG</p>
                <h2 id="catalog-workflow-title">{selectedFlow.method}</h2>
              </div>
              <button
                aria-label="Close workflow form"
                autoFocus
                className="icon-button"
                onClick={() => setSelectedFlow(null)}
                type="button"
              >
                ×
              </button>
            </header>
            <p className="dashboard-workflow-description">{selectedFlow.description}</p>
            <WorkflowForm flow={selectedFlow} />
          </section>
        </div>,
        document.body,
      )}
    </aside>
  );
}

function WorkflowForm({
  flow,
}: {
  flow: ChatFlow;
}) {
  const [values, setValues] = useState<Record<string, string>>(() =>
    Object.fromEntries(flow.fields.map((field) => [
      field.name,
      field.type === "json" ? "[]" : "",
    ])));
  const [error, setError] = useState("");
  const [result, setResult] = useState("");
  const [isRunning, setIsRunning] = useState(false);
  const [pendingParameters, setPendingParameters] = useState<Record<string, unknown> | null>(null);

  function updateField(field: WorkflowParameter, value: string) {
    setValues((current) => ({ ...current, [field.name]: value }));
    setPendingParameters(null);
    setError("");
    setResult("");
  }

  function buildParameters(): Record<string, unknown> {
    const parameters: Record<string, unknown> = {};
    for (const field of flow.fields) {
      const value = values[field.name]?.trim() ?? "";
      if (!value) {
        if (field.required) throw new Error(`${field.name} is required.`);
        continue;
      }

      if (field.type === "uuid" &&
          !/^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(value)) {
        throw new Error(`${field.name} must be a valid UUID.`);
      }
      if (field.type === "number" || field.type === "integer") {
        const number = Number(value);
        if (!Number.isFinite(number) || (field.type === "integer" && !Number.isInteger(number))) {
          throw new Error(`${field.name} must be a valid ${field.type}.`);
        }
        parameters[field.name] = number;
      } else if (field.type === "json" || field.type === "json-string") {
        try {
          JSON.parse(value);
        } catch {
          throw new Error(`${field.name} must contain valid JSON.`);
        }
        parameters[field.name] = field.type === "json-string" ? value : JSON.parse(value) as unknown;
      } else if (field.type === "datetime") {
        const date = new Date(value);
        if (Number.isNaN(date.getTime())) throw new Error(`${field.name} must be a valid date and time.`);
        parameters[field.name] = date.toISOString();
      } else if (field.type === "select" && (value === "true" || value === "false")) {
        parameters[field.name] = value === "true";
      } else {
        parameters[field.name] = value;
      }
    }
    return parameters;
  }

  async function execute(parameters: Record<string, unknown>) {
    setError("");
    setResult("");
    setIsRunning(true);
    try {
      const response = await callBrokerageFlow(flow.method, parameters);
      setResult(JSON.stringify(response.result, null, 2) ?? "null");
      setPendingParameters(null);
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : "The workflow could not be completed.");
    } finally {
      setIsRunning(false);
    }
  }

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError("");
    try {
      const parameters = buildParameters();
      if (flow.isReadOnly) void execute(parameters);
      else setPendingParameters(parameters);
    } catch (validationError) {
      setError(validationError instanceof Error ? validationError.message : "Workflow parameters are invalid.");
    }
  }

  function renderField(field: WorkflowParameter) {
    const label = field.name.replace(/([A-Z])/g, " $1");
    const commonProps = {
      id: `workflow-${flow.method}-${field.name}`,
      onChange: (event: ChangeEvent<HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement>) =>
        updateField(field, event.target.value),
      required: field.required,
      value: values[field.name] ?? "",
    };

    return (
      <label className="workflow-field" htmlFor={commonProps.id} key={field.name}>
        <span>{label}{field.required ? " *" : " (optional)"}</span>
        {field.type === "select" || field.type === "boolean" ? (
          <select {...commonProps}>
            <option value="">Select {label.toLowerCase()}</option>
            {field.options.map((option) => <option key={option} value={option}>{option}</option>)}
          </select>
        ) : field.type === "json" || field.type === "json-string" ? (
          <textarea
            {...commonProps}
            autoCapitalize="off"
            autoCorrect="off"
            placeholder={field.type === "json"
              ? flow.method === "accounts.createJournal"
                ? '[{"type":"Debit","amount":100,"currency":"USD"}]'
                : flow.method === "workflow.start"
                  ? '[{"taskType":"Review","assignedRole":"BrokerageOperations"}]'
                  : "[]"
              : "{\"key\":\"value\"}"}
            spellCheck={false}
          />
        ) : (
          <input
            {...commonProps}
            inputMode={field.type === "number" || field.type === "integer" ? "decimal" : undefined}
            max={field.type === "integer" ? Number.MAX_SAFE_INTEGER : undefined}
            min={field.type === "integer" ? 0 : undefined}
            placeholder={field.type === "uuid" ? "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx" : undefined}
            step={field.type === "integer" ? 1 : field.type === "number" ? "any" : undefined}
            type={field.type === "email" ? "email"
              : field.type === "number" || field.type === "integer" ? "number"
                : field.type === "date" ? "date"
                  : field.type === "datetime" ? "datetime-local" : "text"}
          />
        )}
      </label>
    );
  }

  return (
    <form className="workflow-form" onSubmit={(event) => submit(event)}>
      {flow.fields.length > 0 ? (
        <div className="workflow-fields">{flow.fields.map(renderField)}</div>
      ) : (
        <p className="workflow-no-fields">This workflow does not require parameters.</p>
      )}
      {error && <p className="dashboard-message-error" role="alert">{error}</p>}
      {result && (
        <div className="dashboard-result">
          <span>RESULT</span><pre>{result}</pre>
        </div>
      )}
      {pendingParameters ? (
        <div className="workflow-confirmation" role="alert">
          <p>Run <code>{flow.method}</code>? This operation can change brokerage data.</p>
          <div>
            <button
              className="dashboard-run-button"
              disabled={isRunning}
              onClick={() => void execute(pendingParameters)}
              type="button"
            >
              {isRunning ? "Running…" : "Confirm and run"}
            </button>
            <button
              className="workflow-cancel-button"
              onClick={() => setPendingParameters(null)}
              type="button"
            >
              Cancel
            </button>
          </div>
        </div>
      ) : (
        <button className="dashboard-run-button" disabled={isRunning} type="submit">
          {isRunning ? "Running…" : flow.isReadOnly ? "Run workflow" : "Review operation"}
        </button>
      )}
    </form>
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

function DashboardView({
  identitySession,
  flows,
  flowsError,
  onSignOut,
}: {
  identitySession: IdentityAuthenticationResponse;
  flows: ChatFlow[];
  flowsError: string;
  onSignOut: () => void;
}) {
  const [selectedModule, setSelectedModule] = useState<string | null>(null);
  const [selectedFlow, setSelectedFlow] = useState<ChatFlow | null>(null);
  const modules = [...new Set(flows.map((flow) => flow.method.split(".")[0]))].sort();
  const moduleFlows = selectedModule
    ? flows.filter((flow) => flow.method.startsWith(`${selectedModule}.`))
    : [];
  const readOnlyFlowCount = flows.filter((flow) => flow.isReadOnly).length;

  function chooseFlow(flow: ChatFlow) {
    setSelectedFlow(flow);
  }

  return (
    <div className="dashboard-shell">
      <header className="dashboard-topbar">
        <a className="brand" href="/" aria-label="Brokerage Dashboard home">
          <span className="brand-mark" aria-hidden="true">B</span>
          <span>brokerage<span className="brand-dot">.</span></span>
        </a>
        <div className="dashboard-topbar-actions">
          <span className="dashboard-user">{identitySession.user.email}</span>
          <a className="dashboard-chat-link" href="/chat">Open Chat <span aria-hidden="true">↗</span></a>
          <button className="profile-menu" onClick={onSignOut} type="button">Sign out</button>
        </div>
      </header>

      <div className="dashboard-layout">
        <aside className="dashboard-nav" aria-label="Dashboard navigation">
          <button
            className={`dashboard-nav-item ${selectedModule === null ? "dashboard-nav-item-active" : ""}`}
            onClick={() => {
              setSelectedModule(null);
              setSelectedFlow(null);
            }}
            type="button"
          >
            <span aria-hidden="true">⌂</span> Overview
          </button>
          <p className="dashboard-nav-label">MODULES</p>
          {modules.map((module) => (
            <button
              className={`dashboard-nav-item ${selectedModule === module ? "dashboard-nav-item-active" : ""}`}
              key={module}
              onClick={() => {
                setSelectedModule(module);
                setSelectedFlow(null);
              }}
              type="button"
            >
              <span aria-hidden="true">◇</span>
              <span>{module.replace(/([A-Z])/g, " $1")}</span>
              <span className="dashboard-nav-count">
                {flows.filter((flow) => flow.method.startsWith(`${module}.`)).length}
              </span>
            </button>
          ))}
          {flowsError && <p className="dashboard-nav-error" role="alert">{flowsError}</p>}
        </aside>

        <main className="dashboard-main">
          {selectedModule === null ? (
            <section className="dashboard-overview">
              <p className="dashboard-eyebrow">BROKERAGE WORKSPACE</p>
              <h1>Good to see you.</h1>
              <p className="dashboard-intro">
                Your module workflows are available here. Select a module to view and run its operations.
              </p>
              <div className="dashboard-stats">
                <article className="dashboard-stat">
                  <span>MODULES</span><strong>{modules.length}</strong><small>Available areas</small>
                </article>
                <article className="dashboard-stat">
                  <span>WORKFLOWS</span><strong>{flows.length}</strong><small>Registered operations</small>
                </article>
                <article className="dashboard-stat">
                  <span>READ ONLY</span><strong>{readOnlyFlowCount}</strong><small>Safe lookup operations</small>
                </article>
              </div>
              <div className="dashboard-section-heading">
                <div><p className="dashboard-eyebrow">WORKSPACE</p><h2>Modules</h2></div>
                <span>{modules.length} available</span>
              </div>
              {flowsError ? (
                <p className="dashboard-message dashboard-message-error" role="alert">{flowsError}</p>
              ) : flows.length === 0 ? (
                <p className="dashboard-message" role="status">Loading module workflows…</p>
              ) : (
                <div className="dashboard-module-grid">
                  {modules.map((module) => {
                    const count = flows.filter((flow) => flow.method.startsWith(`${module}.`)).length;
                    return (
                      <button
                        className="dashboard-module-card"
                        key={module}
                        onClick={() => setSelectedModule(module)}
                        type="button"
                      >
                        <span className="dashboard-module-icon" aria-hidden="true">◇</span>
                        <span className="dashboard-module-name">{module.replace(/([A-Z])/g, " $1")}</span>
                        <span className="dashboard-module-count">{count} workflows <i aria-hidden="true">→</i></span>
                      </button>
                    );
                  })}
                </div>
              )}
            </section>
          ) : (
            <section className="dashboard-module-view">
              <button
                className="dashboard-back-link"
                onClick={() => {
                  setSelectedModule(null);
                  setSelectedFlow(null);
                }}
                type="button"
              >
                ← Overview
              </button>
              <p className="dashboard-eyebrow">MODULE WORKFLOWS</p>
              <h1>{selectedModule.replace(/([A-Z])/g, " $1")}</h1>
              <p className="dashboard-intro">Select an operation to view its parameters and execute it.</p>
              {flowsError ? (
                <p className="dashboard-message dashboard-message-error" role="alert">{flowsError}</p>
              ) : (
                <div className="dashboard-workflow-layout">
                  <div className="dashboard-workflow-list" aria-label={`${selectedModule} workflows`}>
                    {moduleFlows.map((flow) => (
                      <button
                        aria-pressed={selectedFlow?.method === flow.method}
                        className={`dashboard-workflow-item ${selectedFlow?.method === flow.method ? "dashboard-workflow-item-active" : ""}`}
                        key={flow.method}
                        onClick={() => chooseFlow(flow)}
                        type="button"
                      >
                        <span className="dashboard-workflow-item-title">{flow.method}</span>
                        <span className={`dashboard-workflow-kind ${flow.isReadOnly ? "dashboard-workflow-read" : "dashboard-workflow-write"}`}>
                          {flow.isReadOnly ? "READ" : "WRITE"}
                        </span>
                        <span className="dashboard-workflow-item-description">{flow.description}</span>
                      </button>
                    ))}
                  </div>
                  {selectedFlow ? (
                    <article className="dashboard-workflow-detail">
                      <div className="dashboard-workflow-detail-heading">
                        <div>
                          <p className="dashboard-eyebrow">WORKFLOW</p>
                          <h2>{selectedFlow.method}</h2>
                        </div>
                        <span className={`dashboard-workflow-kind ${selectedFlow.isReadOnly ? "dashboard-workflow-read" : "dashboard-workflow-write"}`}>
                          {selectedFlow.isReadOnly ? "READ ONLY" : "CHANGES DATA"}
                        </span>
                      </div>
                      <p className="dashboard-workflow-description">{selectedFlow.description}                      </p>
                      <p className="dashboard-parameter-hint">
                        Expected parameters: <code>{selectedFlow.parameters}</code>
                      </p>
                      <WorkflowForm flow={selectedFlow} />
                    </article>
                  ) : (
                    <div className="dashboard-workflow-empty">
                      <span aria-hidden="true">⌁</span>
                      <p>Select a workflow to get started.</p>
                    </div>
                  )}
                </div>
              )}
            </section>
          )}
        </main>
      </div>
    </div>
  );
}

function App() {
  const [identitySession, setIdentitySession] = useState<IdentityAuthenticationResponse | null>(null);
  const [logoutNotice, setLogoutNotice] = useState("");
  const [currentPath, setCurrentPath] = useState(window.location.pathname);
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
    const updatePath = () => setCurrentPath(window.location.pathname);
    window.addEventListener("popstate", updatePath);
    return () => window.removeEventListener("popstate", updatePath);
  }, []);

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
    void getBrokerageFlows()
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
  if (currentPath !== "/chat" && !currentPath.startsWith("/chat/")) {
    return (
      <DashboardView
        flows={flows}
        flowsError={flowsError}
        identitySession={identitySession}
        onSignOut={() => {
          void logoutIdentity().catch((error: unknown) => setLogoutNotice(
            error instanceof Error ? error.message : "Sign out failed.",
          ));
        }}
      />
    );
  }

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
          <a className="nav-item" href="/">
            <span className="nav-icon nav-icon-muted" aria-hidden="true">⌂</span>
            <span>Dashboard</span>
          </a>
          <a className={`nav-item ${activeView === "assistant" ? "nav-item-active" : ""}`} href="/chat">
            <span className="nav-icon" aria-hidden="true">›_</span>
            <span>Assistant</span>
          </a>
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
