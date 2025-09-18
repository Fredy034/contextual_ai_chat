let URL_API = window.URL_API || '/api/chat/ask';
try {
  import('./connection.js').then((mod) => {
    if (mod.default) URL_API = mod.default;
    if (mod.URL_API) URL_API = mod.URL_API;
  });
} catch {}

function parseSimpleMarkdown(text) {
  // Negrilla **texto**
  text = text.replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>');

  // Cursiva *texto*
  text = text.replace(/\*(.+?)\*/g, '<em>$1</em>');

  // Listas ordenadas
  text = text.replace(/(^|\n)(\d+)\. (.+)/g, function (match, p1, p2, p3) {
    return `${p1}<li>${p2}. ${p3}</li>`;
  });

  // Listas no ordenadas
  text = text.replace(/(^|\n)- (.+)/g, function (match, p1, p2) {
    return `${p1}<li>${p2}</li>`;
  });

  // Agrupar <li> en <ul> o <ol>
  text = text.replace(/((<li>\d+\..+<\/li>\s*)+)/g, function (match) {
    return `<ol>${match}</ol>`;
  });
  text = text.replace(/((<li>[^<]+<\/li>\s*)+)/g, function (match) {
    // Si no es una lista ordenada
    if (!/<li>\d+\..+<\/li>/.test(match)) {
      return `<ul>${match}</ul>`;
    }
    return match;
  });

  // Saltos de línea
  text = text.replace(/\n{2,}/g, '<br>');
  text = text.replace(/\n/g, '');
  return text;
}

function addChatMessage(text, sender, format = 'plain') {
  const chat = document.getElementById('chat-messages');
  const message = document.createElement('div');
  message.classList.add('chat-message');
  if (sender === 'user') {
    message.classList.add('chat-user');
    message.innerHTML = text;
  } else {
    message.classList.add('chat-system');
    message.innerHTML = format === 'markdown' ? parseSimpleMarkdown(text) : text;
  }
  chat.appendChild(message);
  chat.scrollTop = chat.scrollHeight;
}

function addLoadingIndicator() {
  const chat = document.getElementById('chat-messages');
  const loadingDiv = document.createElement('div');
  loadingDiv.classList.add('chat-message', 'chat-system', 'chat-loading');
  loadingDiv.setAttribute('id', 'chat-loading-indicator');
  for (let i = 0; i < 3; i++) {
    const dot = document.createElement('span');
    dot.classList.add('chat-dot');
    loadingDiv.appendChild(dot);
  }
  chat.appendChild(loadingDiv);
  chat.scrollTop = chat.scrollHeight;
}

function removeLoadingIndicator() {
  const chat = document.getElementById('chat-messages');
  const loadingIndicator = document.getElementById('chat-loading-indicator');
  if (loadingIndicator) chat.removeChild(loadingIndicator);
}

// Función principal para enviar mensajes
async function sendChatMessage({
  endpoint = URL_API,
  payload = {},
  userText = '',
  format = 'markdown',
  responseKey = 'results',
  minLoadingTime = 1000,
}) {
  if (!userText) return;
  addChatMessage(userText, 'user');
  addLoadingIndicator();
  const startTime = Date.now();
  let responseText = '';
  try {
    const response = await fetch(endpoint, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });
    const data = await response.json();
    responseText = data[responseKey] || data.response || 'Sin respuesta.';
  } catch (err) {
    responseText = 'Error al obtener respuesta.';
  }
  const elapsed = Date.now() - startTime;
  const waitTime = Math.max(0, minLoadingTime - elapsed);
  setTimeout(() => {
    removeLoadingIndicator();
    addChatMessage(responseText, 'system', format);
  }, waitTime);
}

// Integración para search.html/buscarweb.html
if (window.CHAT_TYPE) {
  document.getElementById('user-input').addEventListener('keypress', function (e) {
    if (e.key === 'Enter') {
      const type = window.CHAT_TYPE;
      const userText = document.getElementById('user-input').value.trim();
      const endpoint = type === 'web' ? `${URL_API}/searchweb` : `${URL_API}/search`;
      sendChatMessage({
        endpoint,
        payload: type === 'web' ? { Query: userText } : { Query: userText },
        userText,
        format: 'markdown',
        responseKey: 'results',
      });
      document.getElementById('user-input').value = '';
    }
  });
  window.sendMessage = function (type) {
    const userText = document.getElementById('user-input').value.trim();
    const endpoint = type === 'web' ? `${URL_API}/searchweb` : `${URL_API}/search`;
    sendChatMessage({
      endpoint,
      payload: type === 'web' ? { Query: userText } : { Query: userText },
      userText,
      format: 'markdown',
      responseKey: 'results',
    });
    document.getElementById('user-input').value = '';
  };
}

if (document.getElementById('request-type')) {
  // Custom select logic
  const selectDropdown = document.querySelector('.select-dropdown');
  const selectBtn = document.getElementById('select-btn');
  const selectOptions = document.getElementById('select-options');
  const selectInput = document.getElementById('request-type');

  selectBtn.addEventListener('click', function () {
    selectDropdown.classList.toggle('open');
  });

  selectOptions.querySelectorAll('li').forEach(function (li) {
    li.addEventListener('click', function () {
      selectOptions.querySelectorAll('li').forEach((el) => el.classList.remove('selected'));
      li.classList.add('selected');
      selectInput.value = li.getAttribute('data-value');
      selectDropdown.classList.remove('open');
    });
  });

  document.addEventListener('click', function (e) {
    if (!selectDropdown.contains(e.target)) {
      selectDropdown.classList.remove('open');
    }
  });

  document.getElementById('user-input').addEventListener('keypress', function (e) {
    if (e.key === 'Enter') {
      const userText = document.getElementById('user-input').value.trim();
      const tipo = document.getElementById('request-type').value;
      sendChatMessage({
        endpoint: '/api/chat/ask',
        payload: { Prompt: userText, tipo },
        userText,
        format: tipo == '0' ? 'markdown' : 'plain',
        responseKey: 'response',
      });
      document.getElementById('user-input').value = '';
    }
  });
  window.sendMessage = function () {
    const userText = document.getElementById('user-input').value.trim();
    const tipo = document.getElementById('request-type').value;
    sendChatMessage({
      endpoint: '/api/chat/ask',
      payload: { Prompt: userText, tipo },
      userText,
      format: tipo == '0' ? 'markdown' : 'plain',
      responseKey: 'response',
    });
    document.getElementById('user-input').value = '';
  };
}
