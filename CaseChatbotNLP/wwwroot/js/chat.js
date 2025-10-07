'use strict';
import { URL_API } from './connection.js';
import { getSessionId } from './shared/utils.js';

const conversationHistory = [];

// agrega mensaje al historial respetando límite
function pushHistory(role, content, maxMessages = 20) {
  conversationHistory.push({ role, content });
  while (conversationHistory.length > maxMessages) conversationHistory.shift();
}

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

  // Enlaces [texto](url)
  text = text.replace(/\[([^\]]+)\]\((https?:\/\/[^\s)]+)\)/g, '<a href="$2" target="_blank" rel="noopener">$1</a>');

  // Saltos de línea
  text = text.replace(/\n{2,}/g, '<br>');
  text = text.replace(/\n/g, '');
  return text;
}

function addChatMessage(text, sender, format = 'plain') {
  const chat = document.getElementById('chat-messages');
  const message = document.createElement('div');
  message.classList.add('chat-message');
  let msgContent = '';
  if (sender === 'user') {
    message.classList.add('chat-user');
    msgContent = text;
    pushHistory('user', text);
  } else {
    message.classList.add('chat-system');
    msgContent = format === 'markdown' ? parseSimpleMarkdown(text) : text;
    pushHistory('assistant', text);
  }

  // Contenedor de acciones reutilizable y asignación por tipo de mensaje
  function createActionButton({ iconClass, title, onClick, btnClass = '' }) {
    const btn = document.createElement('button');
    btn.className = btnClass + ' has-tooltip';
    btn.innerHTML = `<i class="${iconClass}"></i><span class="chat-tooltip">${title}</span>`;
    btn.addEventListener('click', onClick);
    return btn;
  }

  // Define los botones disponibles y para qué tipo de mensaje se muestran
  const ACTION_BUTTONS = [
    {
      iconClass: 'fa-solid fa-clone',
      title: 'Copiar',
      btnClass: 'chat-copy-btn',
      showFor: ['both'],
      onClick: function (msgContent, btn) {
        const temp = document.createElement('div');
        temp.innerHTML = msgContent;
        const plainText = temp.textContent || temp.innerText || '';
        navigator.clipboard.writeText(plainText);
        btn.innerHTML = '<i class="fa-solid fa-check"></i>';
        btn.classList.add('copied');
        setTimeout(() => {
          btn.innerHTML = '<i class="fa-solid fa-clone"></i>';
          btn.classList.remove('copied');
        }, 1200);
      },
    },
    {
      iconClass: 'fa-solid fa-rotate',
      title: 'Reintentar',
      btnClass: 'chat-retry-btn',
      showFor: ['system'],
      onClick: function (msgContent, btn) {
        let source = 'index';
        if (window.location.pathname.includes('buscar.html')) source = 'buscar';
        else if (window.location.pathname.includes('buscarweb.html')) source = 'buscarweb';
        let lastUserMsg = '';
        for (let i = conversationHistory.length - 1; i >= 0; i--) {
          if (conversationHistory[i].role === 'user') {
            lastUserMsg = conversationHistory[i].content;
            break;
          }
        }
        if (!lastUserMsg) return;

        const improvedPrompt = lastUserMsg + '\n\nMejora la respuesta anterior.';

        if (source === 'buscar') {
          sendChatMessage({
            endpoint: `${URL_API}/search`,
            payload: { Query: improvedPrompt },
            userText: improvedPrompt,
            format: 'markdown',
            responseKey: 'results',
            skipUserMessage: true,
          });
        } else if (source === 'buscarweb') {
          sendChatMessage({
            endpoint: `${URL_API}/searchweb`,
            payload: { Query: improvedPrompt },
            userText: improvedPrompt,
            format: 'markdown',
            responseKey: 'results',
            skipUserMessage: true,
          });
        } else {
          // index.html
          sendChatMessage({
            endpoint: '/api/chat/ask',
            payload: { Prompt: improvedPrompt, tipo: document.getElementById('request-type')?.value || '0' },
            userText: improvedPrompt,
            format: document.getElementById('request-type')?.value == '0' ? 'markdown' : 'plain',
            responseKey: 'response',
            skipUserMessage: true,
          });
        }
      },
    },
    {
      iconClass: 'fa-solid fa-arrow-up-from-bracket',
      title: 'Compartir chat',
      btnClass: 'chat-share-btn',
      showFor: ['both'],
      onClick: function (msgContent, btn) {
        function stripHtmlAndLinks(html) {
          const temp = document.createElement('div');
          temp.innerHTML = html;
          let text = temp.textContent || temp.innerText || '';

          const linkRegex = /<a [^>]*href="([^"]+)"[^>]*>(.*?)<\/a>/gi;
          let replaced = html.replace(linkRegex, (match, url, label) => {
            return `\n[Enlace] ${label}: ${encodeURI(url)}`;
          });

          temp.innerHTML = replaced;
          text = temp.textContent || temp.innerText || '';

          return text;
        }

        let chatText = conversationHistory
          .map((m) => `${m.role === 'user' ? 'Usuario' : 'Sistema'}: ${stripHtmlAndLinks(m.content)}`)
          .join('\n---\n');
        navigator.clipboard.writeText(chatText).then(() => {
          btn.innerHTML = '<i class="fa-solid fa-check"></i>';
          btn.classList.add('shared');
          setTimeout(() => {
            btn.innerHTML = '<i class="fa-solid fa-arrow-up-from-bracket"></i>';
            btn.classList.remove('shared');
          }, 1200);
        });
      },
    },
    {
      iconClass: 'fa-solid fa-volume-high',
      title: 'Escuchar',
      btnClass: '',
      showFor: ['system'],
      onClick: function (msgContent, btn) {
        if (!('speechSynthesis' in window)) {
          alert('Tu navegador no soporta síntesis de voz.');
          return;
        }
        if (btn.classList.contains('reproducing')) {
          window.speechSynthesis.cancel();
          btn.innerHTML = '<i class="fa-solid fa-volume-high"></i>';
          btn.classList.remove('reproducing');
          return;
        }
        const temp = document.createElement('div');
        temp.innerHTML = msgContent;
        if (temp.textContent.includes('Ver documento')) {
          temp.textContent = temp.textContent.replace('Ver documento', '');
        }
        const utterance = new SpeechSynthesisUtterance(temp.textContent);
        speechSynthesis.speak(utterance);
        btn.innerHTML = '<i class="fa-solid fa-circle-stop"></i>';
        btn.title = 'Detener';
        btn.classList.add('reproducing');
        utterance.onerror = function () {
          btn.innerHTML = '<i class="fa-solid fa-volume-high"></i>';
          btn.classList.remove('reproducing');
        };
        utterance.onend = function () {
          btn.innerHTML = '<i class="fa-solid fa-volume-high"></i>';
          btn.classList.remove('reproducing');
        };
      },
    },
  ];

  const actionsDiv = document.createElement('div');
  actionsDiv.classList.add('chat-actions');

  const typeForBtn = sender === 'user' ? 'user' : 'system';
  ACTION_BUTTONS.forEach((cfg) => {
    if (cfg.showFor.includes(typeForBtn) || cfg.showFor.includes('both')) {
      const btn = createActionButton({
        iconClass: cfg.iconClass,
        title: cfg.title,
        btnClass: cfg.btnClass,
        onClick: function () {
          cfg.onClick(msgContent, btn);
        },
      });
      actionsDiv.appendChild(btn);
    }
  });

  // Estructura final
  const msgBody = document.createElement('div');
  msgBody.classList.add('chat-message-body');
  msgBody.innerHTML = msgContent;
  message.appendChild(msgBody);
  message.appendChild(actionsDiv);
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
  skipUserMessage = false,
}) {
  if (!userText) return;
  if (!skipUserMessage) {
    addChatMessage(userText, 'user');
  }
  addLoadingIndicator();

  const lastUploaded =
    window.__lastUploadedFile && window.__lastUploadedFile.name ? window.__lastUploadedFile.name : null;

  // Construir payload con History limitado (solo History)
  const finalPayload = {
    ...payload,
    Query: userText,
    History: conversationHistory,
    SessionId: getSessionId(),
    UploadFileName: lastUploaded,
  };

  const startTime = Date.now();
  let responseText = '';
  try {
    const response = await fetch(endpoint, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(finalPayload),
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

if (window.CHAT_TYPE) {
  document.getElementById('user-input').addEventListener('keypress', function (e) {
    if (e.key === 'Enter') {
      const type = window.CHAT_TYPE;
      const userText = document.getElementById('user-input').value.trim();
      const endpoint = type === 'web' ? `${URL_API}/searchweb` : `${URL_API}/search`;

      sendChatMessage({
        endpoint,
        payload: { Query: userText },
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
} else {
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

if (document.getElementById('request-type')) {
  const selectDropdown = document.querySelector('.select-dropdown');
  const selectBtn = document.getElementById('select-btn');
  const selectOptions = document.getElementById('select-options');
  const selectInput = document.getElementById('request-type');
  const selectLabel = document.getElementById('select-label');

  selectBtn.addEventListener('click', function () {
    selectDropdown.classList.toggle('open');
  });

  selectOptions.querySelectorAll('li').forEach(function (li) {
    li.addEventListener('click', function () {
      selectOptions.querySelectorAll('li').forEach((el) => el.classList.remove('selected'));
      li.classList.add('selected');
      selectInput.value = li.getAttribute('data-value');
      if (selectLabel) selectLabel.textContent = li.textContent;
      selectDropdown.classList.remove('open');
    });
  });

  document.addEventListener('click', function (e) {
    if (!selectDropdown.contains(e.target)) {
      selectDropdown.classList.remove('open');
    }
  });
}
