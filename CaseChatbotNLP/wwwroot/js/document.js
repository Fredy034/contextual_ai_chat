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

    // Filtro activo
    let activeFilter = window.__docFilter || 'all';

    // Agrupación de extensiones
    const extGroups = {
      img: ['jpg', 'jpeg', 'png', 'gif', 'bmp', 'tiff'],
      pdf: ['pdf'],
      doc: ['doc', 'docx'],
      xls: ['xls', 'xlsx', 'csv'],
      txt: ['txt'],
      other: [], // se asigna después
    };

    // Filtrar documentos
    const filtered = data.filter((d) => {
      if (activeFilter === 'all') return true;
      const fileName = d.fileName ?? d.FileName ?? d.File ?? '';
      const ext = (fileName.split('.').pop() || '').toLowerCase();
      if (extGroups[activeFilter]) return extGroups[activeFilter].includes(ext);
      // Otros: si no está en ningún grupo
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
        let extClass = '';
        if (extGroups.txt.includes(ext)) extClass = 'doc-txt';
        else if (extGroups.pdf.includes(ext)) extClass = 'doc-pdf';
        else if (extGroups.doc.includes(ext)) extClass = 'doc-doc';
        else if (extGroups.xls.includes(ext)) extClass = 'doc-xls';
        else if (extGroups.img.includes(ext)) extClass = 'doc-img';
        else extClass = 'doc-other';

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
    const filterBtns = ['filterAll', 'filterImg', 'filterPdf', 'filterDoc', 'filterXls', 'filterTxt', 'filterOther'];
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
