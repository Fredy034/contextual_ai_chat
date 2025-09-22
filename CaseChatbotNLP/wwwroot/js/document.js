'use strict';
import { URL_API } from './connection.js';

export async function loadDocuments(containerId = 'documentsList') {
  const container = document.getElementById(containerId);
  if (!container) return;

  container.innerHTML = `<p class="muted">Cargando documentos...</p>`;

  try {
    const resp = await fetch(`${URL_API}/documents`);
    if (!resp.ok) throw new Error('No se pudo obtener la lista de documentos');

    const data = await resp.json();

    if (!Array.isArray(data) || data.length === 0) {
      container.innerHTML = '<p class="muted">No hay documentos aún.</p>';
      return;
    }

    container.innerHTML = data
      .map((d) => {
        // Nombre y snippet (soporte distintos formatos de la respuesta)
        const fileName = d.fileName ?? d.FileName ?? d.File ?? 'sin-nombre';
        const snippet = d.snippet ?? d.Snippet ?? d.SnippetText ?? '';

        // Extensión y clase
        const ext = (fileName.split('.').pop() || '').toLowerCase();
        let extClass = '';
        if (['txt'].includes(ext)) extClass = 'doc-txt';
        else if (['pdf'].includes(ext)) extClass = 'doc-pdf';
        else if (['doc', 'docx'].includes(ext)) extClass = 'doc-doc';
        else if (['xls', 'xlsx', 'csv'].includes(ext)) extClass = 'doc-xls';
        else extClass = 'doc-other';

        // URLs: prefer previewUrl/downloadUrl del backend, si no existen construirlas
        const previewUrl = d.previewUrl ?? `${URL_API}/download?name=${encodeURIComponent(fileName)}`;
        const downloadUrl = d.downloadUrl ?? `${URL_API}/download?name=${encodeURIComponent(fileName)}&download=true`;

        // Codificar la previewUrl para evitar problemas con comillas en el atributo onclick
        const previewUrlEncoded = encodeURIComponent(previewUrl);

        return `
        <article class="doc-card ${extClass}">
          <div class="doc-main">
            <h3 class="doc-title">${escapeHtml(fileName)}</h3>
            <p class="doc-snippet">${escapeHtml(snippet)}</p>
          </div>
          <div class="doc-actions">
            <a class="btn-secondary" href="${escapeAttr(downloadUrl)}" rel="noopener" download>Descargar</a>
            <button class="btn-tertiary" onclick="window.open(decodeURIComponent('${previewUrlEncoded}'), '_blank', 'noopener')">Vista</button>
          </div>
        </article>
      `;
      })
      .join('');
  } catch (err) {
    container.innerHTML = `<p class="error">Error: ${escapeHtml(err.message)}</p>`;
  }
}

// helper export for upload to refresh list
export async function refreshDocuments() {
  await loadDocuments();
}

// small helper to escape HTML (prevent XSS al mostrar nombres/textos)
function escapeHtml(unsafe) {
  if (!unsafe) return '';
  return String(unsafe)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#039;');
}

// helper to escape attributes (prevent XSS al mostrar URLs)
function escapeAttr(s) {
  if (!s) return '';
  return String(s).replace(/"/g, '&quot;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}
