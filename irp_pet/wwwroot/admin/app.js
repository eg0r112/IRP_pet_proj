const API = '/api/v1';
const STORAGE_KEY = 'irp_admin_token';

const loginView = document.getElementById('login-view');
const dashboardView = document.getElementById('dashboard-view');
const loginForm = document.getElementById('login-form');
const loginError = document.getElementById('login-error');
const userInfo = document.getElementById('user-info');
const logoutBtn = document.getElementById('logout-btn');

function getToken() {
  return localStorage.getItem(STORAGE_KEY);
}

function setToken(token) {
  if (token) localStorage.setItem(STORAGE_KEY, token);
  else localStorage.removeItem(STORAGE_KEY);
}

function show(el) { el.classList.remove('hidden'); }
function hide(el) { el.classList.add('hidden'); }

function setMessage(el, text, ok) {
  el.textContent = text;
  el.classList.remove('hidden', 'ok', 'err');
  el.classList.add(ok ? 'ok' : 'err');
}

async function api(path, options = {}) {
  const headers = { 'Content-Type': 'application/json', ...(options.headers || {}) };
  const token = getToken();
  if (token) headers.Authorization = `Bearer ${token}`;

  const response = await fetch(`${API}${path}`, { ...options, headers });
  const text = await response.text();
  let data = null;
  try { data = text ? JSON.parse(text) : null; } catch { data = text; }

  if (!response.ok) {
    const message = data?.message || data?.title || `HTTP ${response.status}`;
    throw new Error(message);
  }
  return data;
}

function toLocalInputValue(date) {
  const pad = (n) => String(n).padStart(2, '0');
  return `${date.getUTCFullYear()}-${pad(date.getUTCMonth() + 1)}-${pad(date.getUTCDate())}T${pad(date.getUTCHours())}:${pad(date.getUTCMinutes())}`;
}

function fromLocalInputValue(value) {
  return new Date(`${value}:00Z`).toISOString();
}

function showDashboard(email) {
  hide(loginView);
  show(dashboardView);
  userInfo.textContent = email ? `· ${email}` : '';
}

function showLogin() {
  setToken(null);
  show(loginView);
  hide(dashboardView);
}

loginForm.addEventListener('submit', async (e) => {
  e.preventDefault();
  hide(loginError);
  try {
    const email = document.getElementById('login-email').value.trim();
    const password = document.getElementById('login-password').value;
    const result = await api('/auth/login', {
      method: 'POST',
      body: JSON.stringify({ email, password })
    });
    if (result.role !== 'admin') {
      throw new Error('Доступ только для роли admin');
    }
    setToken(result.accessToken);
    showDashboard(email);
    await loadUsers();
    await loadOnCall();
  } catch (err) {
    loginError.textContent = err.message;
    show(loginError);
  }
});

logoutBtn.addEventListener('click', () => showLogin());

document.querySelectorAll('.tab').forEach((tab) => {
  tab.addEventListener('click', () => {
    document.querySelectorAll('.tab').forEach((t) => t.classList.remove('active'));
    document.querySelectorAll('.tab-panel').forEach((p) => p.classList.remove('active'));
    tab.classList.add('active');
    document.getElementById(`tab-${tab.dataset.tab}`).classList.add('active');
  });
});

document.getElementById('ping-btn').addEventListener('click', async () => {
  const box = document.getElementById('ping-result');
  box.textContent = 'Загрузка...';
  try {
    const data = await api('/admin/ping');
    box.textContent = JSON.stringify(data, null, 2);
  } catch (err) {
    box.textContent = err.message;
  }
});

async function loadOnCall() {
  const box = document.getElementById('oncall-current');
  box.textContent = 'Загрузка...';
  try {
    const data = await api('/admin/oncall/current');
    if (!Array.isArray(data) || data.length === 0) {
      box.textContent = 'Нет активных смен';
      return;
    }
    box.innerHTML = data.map((shift) => `
      <div class="oncall-card">
        <div><strong>${shift.displayName}</strong> · ${shift.email}</div>
        <div>Роль: ${shift.role || '—'} · Telegram: ${shift.telegramChatId || '—'}</div>
        <div>Смена: ${new Date(shift.shiftStartsAtUtc).toLocaleString()} — ${new Date(shift.shiftEndsAtUtc).toLocaleString()}</div>
      </div>
    `).join('');
  } catch (err) {
    box.textContent = err.message;
  }
}

document.getElementById('refresh-oncall-btn').addEventListener('click', loadOnCall);

document.getElementById('shift-form').addEventListener('submit', async (e) => {
  e.preventDefault();
  const msg = document.getElementById('shift-message');
  try {
    await api('/admin/oncall/shifts', {
      method: 'POST',
      body: JSON.stringify({
        userId: document.getElementById('shift-user').value,
        startsAtUtc: fromLocalInputValue(document.getElementById('shift-start').value),
        endsAtUtc: fromLocalInputValue(document.getElementById('shift-end').value),
        note: document.getElementById('shift-note').value || null
      })
    });
    setMessage(msg, 'Смена создана', true);
    await loadOnCall();
  } catch (err) {
    setMessage(msg, err.message, false);
  }
});

async function loadUsers() {
  const users = await api('/admin/users');
  const tbody = document.getElementById('users-table');
  const select = document.getElementById('shift-user');

  tbody.innerHTML = users.map((u) => `
    <tr>
      <td>${u.email}</td>
      <td>${u.displayName}</td>
      <td>${u.role}</td>
      <td>${u.telegramChatId || '—'}</td>
    </tr>
  `).join('');

  const oncallUsers = users.filter((u) => u.role === 'admin' || u.role === 'oncall');
  select.innerHTML = oncallUsers.map((u) =>
    `<option value="${u.id}">${u.displayName} (${u.email})</option>`
  ).join('');
}

document.getElementById('refresh-users-btn').addEventListener('click', async () => {
  try { await loadUsers(); } catch (err) { alert(err.message); }
});

document.getElementById('user-form').addEventListener('submit', async (e) => {
  e.preventDefault();
  const msg = document.getElementById('user-message');
  try {
    await api('/admin/users', {
      method: 'POST',
      body: JSON.stringify({
        email: document.getElementById('user-email').value.trim(),
        password: document.getElementById('user-password').value,
        displayName: document.getElementById('user-display').value.trim(),
        role: document.getElementById('user-role').value,
        telegramChatId: document.getElementById('user-telegram').value.trim() || null
      })
    });
    setMessage(msg, 'Пользователь создан', true);
    e.target.reset();
    await loadUsers();
  } catch (err) {
    setMessage(msg, err.message, false);
  }
});

function initShiftDefaults() {
  const start = new Date();
  const end = new Date();
  end.setUTCDate(end.getUTCDate() + 7);
  document.getElementById('shift-start').value = toLocalInputValue(start);
  document.getElementById('shift-end').value = toLocalInputValue(end);
}

if (getToken()) {
  showDashboard('');
  loadUsers().catch(showLogin);
  loadOnCall().catch(() => {});
  initShiftDefaults();
} else {
  showLogin();
  initShiftDefaults();
}
