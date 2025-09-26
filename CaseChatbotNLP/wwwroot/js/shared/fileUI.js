import { escapeHtml, shortName, iconForExt } from './utils.js';
import { uploadFileForSession } from '../upload.js';

/**
 * options:
 *  - fileInputId
 *  - addContextId
 *  - uploadBannerId
 *  - uploadedFileInfoId
 *  - chatMessagesId
 *  - inputGroupSelector
 */
export function initUploadUI(options = {}) {
  const fileInput = document.getElementById(options.fileInputId || 'fileInput');
  const addContext = document.getElementById(options.addContextId || 'addContextLi');
  const bannerEl = document.getElementById(options.uploadBannerId || 'uploadBanner');
  const infoDiv = document.getElementById(options.uploadedFileInfoId || 'uploadedFileInfo');
  const inputGroup = document.querySelector(options.inputGroupSelector || '.input-group');
  const chatMessages = document.getElementById(options.chatMessagesId || 'chat-messages');

  if (!fileInput) return console.warn('initUploadUI: fileInput no encontrado');

  window.__lastUploadedFile = window.__lastUploadedFile || null;

  if (addContext) {
    addContext.addEventListener('click', () => fileInput.click());
  }

  fileInput.addEventListener('change', async function () {
    const file = this.files && this.files[0] ? this.files[0] : null;
    if (!file) {
      clearUploadedFileUI();
      hideBanner();
      return;
    }

    showBannerUploading(file);
    renderUploadedFilePreview(file);

    const result = await uploadFileForSession(file);

    if (!result.ok) {
      showBannerError(result.error || 'Error al subir archivo');
      return;
    }

    const filename = file.name;
    window.__lastUploadedFile = { name: filename, serverMessage: result.data?.message ?? '' };

    renderUploadedFileUI(window.__lastUploadedFile);
    showBannerSuccess(`Archivo agregado: <strong>${escapeHtml(shortName(filename))}</strong>`, result.data?.message);
  });

  return {
    renderUploadedFileUI: (meta) => renderUploadedFileUI(meta),
    clearUploadedFileUI,
    showBanner,
    hideBanner,
  };

  // ---------- helpers ----------
  function showBanner(contentHtml, type = 'info') {
    if (!bannerEl) return;
    const base =
      `<div class="banner-inner">` +
      `<div class="banner-left">${contentHtml}</div>` +
      `<div class="banner-actions">
        <button id="bannerCloseBtn" aria-label="Cerrar banner">
          <i class="fa-solid fa-xmark"></i>
        </button>
      </div>`;
    bannerEl.innerHTML = base;
    bannerEl.style.display = 'block';
    bannerEl.classList.remove('info', 'success', 'error');

    if (type === 'info') bannerEl.classList.add('info');
    else if (type === 'success') bannerEl.classList.add('success');
    else if (type === 'error') bannerEl.classList.add('error');

    const closeBtn = document.getElementById('bannerCloseBtn');
    if (closeBtn) {
      closeBtn.addEventListener('click', () => hideBanner());
    }

    if (window.__bannerTimeout) clearTimeout(window.__bannerTimeout);
    window.__bannerTimeout = setTimeout(() => hideBanner(), 10000);
  }

  function hideBanner() {
    if (!bannerEl) return;
    bannerEl.style.display = 'none';
    bannerEl.innerHTML = '';
  }

  function showBannerUploading(file) {
    const name = escapeHtml(shortName(file.name));
    const html = `<span class="spinner-container"><span class="spinner" aria-hidden="true"></span> Subiendo <strong>${name}</strong>...</span>`;
    showBanner(html, 'info');
  }

  function showBannerSuccess(titleHtml, serverMsg) {
    const msgHtml = serverMsg ? `<div class="server-msg">${escapeHtml(serverMsg)}</div>` : '';
    const content = `<div>${titleHtml}${msgHtml}</div>`;
    showBanner(content, 'success');
  }

  function showBannerError(msg) {
    const content = `<span>❌ ${escapeHtml(msg)}</span>`;
    showBanner(content, 'error');
  }

  function renderUploadedFilePreview(file) {
    if (!infoDiv) return;
    const name = shortName(file.name);
    const ext = (file.name.split('.').pop() || '').toLowerCase();
    const { icon, classIcon } = iconForExt(ext);

    infoDiv.innerHTML = `
      <i class="fa-solid ${icon} doc-icon ${classIcon}"></i>
      <span class="uploaded-file-name">${escapeHtml(name)}</span>
    `;
    if (infoDiv.parentElement) infoDiv.parentElement.style.display = 'block';
    if (inputGroup) inputGroup.classList.add('with-file');
  }

  function renderUploadedFileUI(fileMeta) {
    if (!infoDiv) return;
    if (!fileMeta) return clearUploadedFileUI();

    let name = fileMeta.name;
    if (name.length > 32) name = name.slice(0, 16) + '...' + name.slice(-12);
    const ext = (fileMeta.name.split('.').pop() || '').toLowerCase();
    const { icon, classIcon } = iconForExt(ext);

    infoDiv.innerHTML = `
      <i class="fa-solid ${icon} doc-icon ${classIcon}"></i>
      <span class="uploaded-file-name">${name}</span>
      <button id="removeFileBtn" class="btn-remove-file" title="Quitar Archivo">
        <i class="fa-solid fa-xmark"></i>
      </button>
    `;
    if (infoDiv.parentElement) infoDiv.parentElement.style.display = 'block';
    if (inputGroup) inputGroup.classList.add('with-file');
    if (inputGroup && inputGroup.classList.contains('with-file')) chatMessages?.classList.add('with-file');

    const btn = document.getElementById('removeFileBtn');
    if (btn) {
      btn.addEventListener('click', function () {
        fileInput.value = '';
        clearUploadedFileUI();
        hideBanner();
        window.__lastUploadedFile = null;
      });
    }
  }

  function clearUploadedFileUI() {
    if (!infoDiv) return;
    infoDiv.innerHTML = '';
    if (infoDiv.parentElement) infoDiv.parentElement.style.display = 'none';
    if (inputGroup) inputGroup.classList.remove('with-file');
    if (chatMessages) chatMessages.classList.remove('with-file');
  }
}
