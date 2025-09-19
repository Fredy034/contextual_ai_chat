// js/upload.js
'use strict';
import { URL_API } from './connection.js';
import { refreshDocuments } from './document.js';

const fileInput = document.getElementById('fileInput');
const fileNameSpan = document.getElementById('fileName');
const statusElement = document.getElementById('uploadStatus');

if (fileInput && fileNameSpan) {
  fileInput.addEventListener('change', function () {
    if (fileInput.files && fileInput.files[0]) {
      fileNameSpan.textContent = fileInput.files[0].name;
    } else {
      fileNameSpan.textContent = '';
    }
  });
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
    const response = await fetch(`${URL_API}/upload`, {
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
  } catch (error) {
    statusElement.textContent = `❌ Error: ${error.message}`;
    fileNameSpan.textContent = '';
  }
}
