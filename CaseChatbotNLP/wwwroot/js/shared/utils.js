export function escapeHtml(unsafe) {
  if (!unsafe) return '';
  return String(unsafe)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#039;');
}

export function escapeAttr(s) {
  if (!s) return '';
  return String(s).replace(/"/g, '&quot;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}

export function shortName(full, max = 32) {
  if (!full) return '';
  if (full.length <= max) return full;
  const head = Math.floor(max / 2) - 2;
  const tail = Math.ceil(max / 2);
  return full.slice(0, head) + '...' + full.slice(-tail);
}

export function iconForExt(ext) {
  ext = (ext || '').toLowerCase();
  if (['xls', 'xlsx', 'csv'].includes(ext)) return { icon: 'fa-file-excel', classIcon: 'doc-xls' };
  if (['doc', 'docx'].includes(ext)) return { icon: 'fa-file-word', classIcon: 'doc-doc' };
  if (['jpg', 'jpeg', 'png', 'bmp', 'tiff'].includes(ext)) return { icon: 'fa-file-image', classIcon: 'doc-img' };
  if (ext === 'pdf') return { icon: 'fa-file-pdf', classIcon: 'doc-pdf' };
  if (ext === 'txt') return { icon: 'fa-file-lines', classIcon: 'doc-txt' };
  return { icon: 'fa-file', classIcon: 'doc-other' };
}

export function getSessionId(storageKey = 'chatSessionId') {
  let sid = localStorage.getItem(storageKey);
  if (!sid) {
    if (crypto && crypto.randomUUID) sid = crypto.randomUUID();
    else sid = 'sid-' + Date.now() + '-' + Math.floor(Math.random() * 10000);
    localStorage.setItem(storageKey, sid);
  }
  return sid;
}

export function buildHistoryText(history = [], maxChars = 6000) {
  let text = history.map((m) => `${m.role.toUpperCase()}: ${m.content}`).join('\n---\n');
  if (text.length <= maxChars) return text;
  return text.slice(-maxChars);
}

export function parseContentDispositionFilename(contentDispositionHeader) {
  if (!contentDispositionHeader) return null;
  const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(contentDispositionHeader);
  return match && match[1] ? decodeURIComponent(match[1]) : null;
}

export function downloadBlob(blob, filename = 'file') {
  const url = window.URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  a.remove();
  window.URL.revokeObjectURL(url);
}
