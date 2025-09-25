'use strict';
import { URL_API } from './connection.js';
import { refreshDocuments } from './document.js';

const fileInput = document.getElementById('fileInput');
const fileNameSpan = document.getElementById('fileName');
const statusElement = document.getElementById('uploadStatus');

const btnSave = document.getElementById('btnSaveCase');
const statusEl = document.getElementById('caseStatus');

if (btnSave) btnSave.addEventListener('click', submitCaseForm);

if (fileInput && fileNameSpan) {
  fileInput.addEventListener('change', function () {
    if (fileInput.files && fileInput.files[0]) {
      fileNameSpan.textContent = fileInput.files[0].name;
    } else {
      fileNameSpan.textContent = '';
    }
  });
}

export async function uploadFileForSession(file) {
  if (!file) return { ok: false, error: 'No file provided' };

  const formData = new FormData();
  formData.append('file', file);

  try {
    const sessionId = localStorage.getItem('chatSessionId') || '';
    const url = sessionId ? `${URL_API}/upload?sessionId=${encodeURIComponent(sessionId)}` : `${URL_API}/upload`;

    const response = await fetch(url, {
      method: 'POST',
      body: formData,
    });

    if (!response.ok) {
      const text = await response.text();
      return { ok: false, error: text || 'Error en la subida' };
    }

    const data = await response.json();

    return { ok: true, data };
  } catch (err) {
    return { ok: false, error: err.message || String(err) };
  }
}

export async function uploadFile() {
  statusElement.textContent = '';
  if (!fileInput || !fileInput.files[0]) {
    statusElement.textContent = '⚠️ No has seleccionado ningún archivo.';
    fileNameSpan.textContent = '';
    return;
  }

  // Mostrar mensaje de carga
  statusElement.innerHTML =
    '<span class="upload-loading"><span class="upload-dot"></span><span class="upload-dot"></span><span class="upload-dot"></span> Subiendo archivo...</span>';

  const formData = new FormData();
  formData.append('file', fileInput.files[0]);

  try {
    const sessionId = localStorage.getItem('chatSessionId') || '';
    const url = sessionId ? `${URL_API}/upload?sessionId=${encodeURIComponent(sessionId)}` : `${URL_API}/upload`;

    const response = await fetch(url, {
      method: 'POST',
      body: formData,
    });

    if (!response.ok) {
      const text = await response.text();
      throw new Error(text || 'Error en la subida');
    }

    const data = await response.json();
    statusElement.textContent = `✅ ${data.message ?? 'Archivo subido.'}`;

    // Limpiar input y nombre
    fileInput.value = '';
    fileNameSpan.textContent = '';

    // Refrescar lista de documentos
    await refreshDocuments();

    // Ocultar formulario de subida
    const uploadSection = document.getElementById('uploadSection');
    if (uploadSection) uploadSection.style.display = 'none';
  } catch (error) {
    statusElement.textContent = `❌ Error: ${error.message}`;
    fileNameSpan.textContent = '';
  }
}

async function submitCaseForm() {
  statusEl.textContent = '';
  const desc = document.getElementById('descError').value.trim();
  const services = document.getElementById('services').value.trim();
  const steps = document.getElementById('steps').value.trim();
  const cont = document.getElementById('continueWork').value.trim();
  const solution = document.getElementById('solution').value.trim();

  if (!desc || !steps || !solution) {
    statusEl.textContent = '⚠️ Completa campos obligatorios: Descripción, Pasos, Solución.';
    return;
  }

  // Formato del .txt (puedes adaptarlo)
  const content = [
    'Descripción del Error:',
    desc,
    '',
    'Servicios impactados:',
    services,
    '',
    'Pasos para reproducir el error:',
    steps,
    '',
    'Cómo se continuó el trabajo mientras se resolvía:',
    cont,
    '',
    'Solución del error:',
    solution,
    '',
  ].join('\n');

  // límite opcional (evitar textos enormes)
  if (content.length > 300_000) {
    statusEl.textContent = '⚠️ Caso demasiado extenso (máx 300KB).';
    return;
  }

  // Nombre de archivo único
  const ts = new Date().toISOString().replace(/[:.]/g, '-');
  const filename = `errores_conocidos_${ts}.txt`;
  const file = new File([content], filename, { type: 'text/plain' });

  const formData = new FormData();
  formData.append('file', file);

  try {
    statusEl.innerHTML = 'Subiendo caso...';
    const resp = await fetch(`${URL_API}/upload`, {
      method: 'POST',
      body: formData,
    });

    if (!resp.ok) {
      const text = await resp.text();
      throw new Error(text || 'Error subiendo el caso');
    }

    const data = await resp.json();
    statusEl.textContent = `✅ Caso guardado: ${data.message ?? filename}`;
    // limpiar formulario
    document.getElementById('caseForm').reset();
    // refrescar lista de documentos para que aparezca el .txt nuevo
    await refreshDocuments();

    // Ocultar formulario de caso
    const caseSection = document.getElementById('caseSection');
    if (caseSection) caseSection.style.display = 'none';
  } catch (err) {
    statusEl.textContent = `❌ Error: ${err.message}`;
  }
}
