// js/download.js
'use strict';
import { URL_API } from './connection.js';

export async function downloadFile() {
  const fileId = document.getElementById('fileId').value.trim();
  const statusElement = document.getElementById('downloadStatus');

  statusElement.textContent = '';
  if (!fileId) {
    statusElement.textContent = '⚠️ Ingresa un nombre de archivo.';
    return;
  }

  try {
    const response = await fetch(`${URL_API}/download?name=${encodeURIComponent(fileId)}`, {
      method: 'GET',
    });

    if (!response.ok) throw new Error('Archivo no encontrado');

    const blob = await response.blob();
    const contentDisposition = response.headers.get('content-disposition') || '';
    let filename = `archivo-${fileId}`;

    // intentar extraer nombre real del header content-disposition
    const match = /filename="?([^"]+)"?/.exec(contentDisposition);
    if (match && match[1]) filename = match[1];

    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    a.remove();
    window.URL.revokeObjectURL(url);

    statusElement.textContent = '✅ Descarga iniciada.';
  } catch (error) {
    statusElement.textContent = `❌ Error: ${error.message}`;
  }
}
