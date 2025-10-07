/**
 * An object that groups file extensions by their type.
 *
 * @typedef {Object} ExtGroups
 * @property {string[]} img - Image file extensions.
 * @property {string[]} pdf - PDF file extensions.
 * @property {string[]} doc - Document file extensions (Word).
 * @property {string[]} xls - Spreadsheet file extensions (Excel, CSV).
 * @property {string[]} txt - Text file extensions.
 * @property {string[]} other - Other file extensions (currently empty).
 *
 * @type {ExtGroups}
 */
export const extGroups = {
  img: ['jpg', 'jpeg', 'png', 'gif', 'bmp', 'tiff'],
  pdf: ['pdf'],
  doc: ['doc', 'docx'],
  xls: ['xls', 'xlsx', 'csv'],
  txt: ['txt'],
  video: ['mp4', 'mkv', 'webm'],
  other: [], 
};

/**
 * Escapes special HTML characters in a string to prevent XSS attacks.
 *
 * Converts characters like &, <, >, ", and ' into their corresponding HTML entities.
 *
 * @param {string} unsafe - The string to be escaped.
 * @returns {string} The escaped string safe for HTML rendering.
 */
export function escapeHtml(unsafe) {
  if (!unsafe) return '';

  return String(unsafe)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#039;');
}

/**
 * Escapes special characters in a string for safe use in HTML attribute values.
 *
 * Replaces double quotes (`"`), less-than (`<`), and greater-than (`>`) characters
 * with their corresponding HTML entities.
 *
 * @param {string} s - The string to escape.
 * @returns {string} The escaped string safe for use in HTML attributes.
 */
export function escapeAttr(s) {
  if (!s) return '';

  return String(s).replace(/"/g, '&quot;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}

/**
 * Shortens a string to a specified maximum length by truncating the middle and adding ellipsis.
 *
 * @param {string} full - The original string to be shortened.
 * @param {number} [max=32] - The maximum allowed length of the returned string.
 * @returns {string} The shortened string with ellipsis if truncation occurred, otherwise the original string.
 */
export function shortName(full, max = 32) {
  if (!full) return '';
  if (full.length <= max) return full;
  const head = Math.floor(max / 2) - 2;
  const tail = Math.ceil(max / 2);

  return full.slice(0, head) + '...' + full.slice(-tail);
}

/**
 * Returns the appropriate icon and CSS class for a given file extension.
 *
 * @param {string} ext - The file extension (e.g., 'pdf', 'docx', 'jpg').
 * @returns {{icon: string, classIcon: string}} An object containing the icon class and CSS class for the file type.
 */
export function iconForExt(ext) {
  ext = (ext || '').toLowerCase();
  if (extGroups.xls.includes(ext)) return { icon: 'fa-file-excel', classIcon: 'doc-xls' };
  if (extGroups.doc.includes(ext)) return { icon: 'fa-file-word', classIcon: 'doc-doc' };
  if (extGroups.img.includes(ext)) return { icon: 'fa-file-image', classIcon: 'doc-img' };
  if (extGroups.pdf.includes(ext)) return { icon: 'fa-file-pdf', classIcon: 'doc-pdf' };
  if (extGroups.txt.includes(ext)) return { icon: 'fa-file-lines', classIcon: 'doc-txt' };
  if (extGroups.video.includes(ext)) return { icon: 'fa-file-video', classIcon: 'doc-video' };

  return { icon: 'fa-file', classIcon: 'doc-other' };
}

/**
 * Retrieves a session ID from localStorage using the specified storage key.
 * If no session ID exists, generates a new one using `crypto.randomUUID` if available,
 * otherwise falls back to a timestamp and random number, then stores it in localStorage.
 *
 * @param {string} [storageKey='chatSessionId'] - The key used to store/retrieve the session ID from localStorage.
 * @returns {string} The session ID.
 */
export function getSessionId(storageKey = 'chatSessionId') {
  let sid = localStorage.getItem(storageKey);
  if (!sid) {
    if (crypto && crypto.randomUUID) sid = crypto.randomUUID();
    else sid = 'sid-' + Date.now() + '-' + Math.floor(Math.random() * 10000);
    localStorage.setItem(storageKey, sid);
  }

  return sid;
}

/**
 * Builds a formatted text representation of a chat history, optionally truncating to a maximum character length.
 *
 * @param {Array<{role: string, content: string}>} history - Array of message objects, each containing a role and content.
 * @param {number} [maxChars=6000] - Maximum number of characters allowed in the output text.
 * @returns {string} Formatted chat history as a string, truncated from the end if it exceeds maxChars.
 */
export function buildHistoryText(history = [], maxChars = 6000) {
  let text = history.map((m) => `${m.role.toUpperCase()}: ${m.content}`).join('\n---\n');
  if (text.length <= maxChars) return text;

  return text.slice(-maxChars);
}

/**
 * Extracts and decodes the filename from a Content-Disposition header.
 *
 * @param {string} contentDispositionHeader - The Content-Disposition header value.
 * @returns {string|null} The decoded filename if found, otherwise null.
 */
export function parseContentDispositionFilename(contentDispositionHeader) {
  if (!contentDispositionHeader) return null;
  const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(contentDispositionHeader);

  return match && match[1] ? decodeURIComponent(match[1]) : null;
}

/**
 * Initiates a download of a given Blob object as a file with the specified filename.
 *
 * @param {Blob} blob - The Blob object to be downloaded.
 * @param {string} [filename='file'] - The name for the downloaded file.
 */
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
