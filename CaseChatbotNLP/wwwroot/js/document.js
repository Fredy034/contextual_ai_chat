'use strict';
import { URL_API } from './connection.js';
import { escapeAttr, escapeHtml, iconForExt, extGroups } from './shared/utils.js';

export async function loadDocuments(containerId = 'documentsList') {
  const container = document.getElementById(containerId);
  if (!container) return;

  container.innerHTML = `<p class="muted">Cargando documentos...</p>`;

  try {
    const resp = await fetch(`${URL_API}/documents`);
    if (!resp.ok) throw new Error('No se pudo obtener la lista de documentos');

    const data = await resp.json();
    // console.log("Contenido:", data.length);

    if (!Array.isArray(data) || data.length === 0) {
      container.innerHTML = '<p class="muted">No hay documentos aún.</p>';
      return;
    }

    let activeFilter = window.__docFilter || 'all';

    const filtered = data.filter((d) => {
      if (activeFilter === 'all') return true;
      const fileName = d.fileName ?? d.FileName ?? d.File ?? '';
      const ext = (fileName.split('.').pop() || '').toLowerCase();
      if (extGroups[activeFilter]) return extGroups[activeFilter].includes(ext);
      if (activeFilter === 'other') {
        return !Object.values(extGroups).flat().includes(ext);
      }
      return true;
    });

    if (filtered.length === 0) {
      container.innerHTML = '<p class="muted">No hay documentos para este filtro.</p>';
      return;
    }

    container.innerHTML = filtered
      .map((d) => {
        const fileName = d.fileName ?? d.FileName ?? d.File ?? 'sin-nombre';
        const snippet = d.snippet ?? d.Snippet ?? d.SnippetText ?? '';
        const ext = (fileName.split('.').pop() || '').toLowerCase();
        const extClass = iconForExt(ext).classIcon;

        const previewUrl = d.previewUrl ?? `${URL_API}/download?name=${encodeURIComponent(fileName)}`;
        const downloadUrl = d.downloadUrl ?? `${URL_API}/download?name=${encodeURIComponent(fileName)}&download=true`;
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

  // Conectar filtros
  if (!window.__docFilterInit) {
    window.__docFilterInit = true;
    const filterBtns = ['filterAll', 'filterImg', 'filterPdf', 'filterDoc', 'filterXls', 'filterTxt', 'filterVideo', 'filterOther'];
    filterBtns.forEach((id) => {
      const btn = document.getElementById(id);
      if (btn) {
        btn.addEventListener('click', () => {
          window.__docFilter = btn.getAttribute('data-filter');
          filterBtns.forEach((bid) => {
            const b = document.getElementById(bid);
            if (b) b.classList.remove('active');
          });
          btn.classList.add('active');
          loadDocuments(containerId);
        });
      }
    });
    // Marcar "Todos" como activo por defecto
    const allBtn = document.getElementById('filterAll');
    if (allBtn) allBtn.classList.add('active');
  }
}

export async function refreshDocuments() {
  await loadDocuments();
}
