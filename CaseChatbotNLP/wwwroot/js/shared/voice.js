// API: initVoice(options) -> controller
// controller: { supported, start(), stop(), toggle(), destroy() }
export function initVoice({
  voiceBtnId,
  voiceIconId,
  inputId,
  lang = 'es-ES',
  continuous = false,
  interimResults = false,
  autoShowIfUnsupported = false,
  onResult = null,
  onStart = null,
  onEnd = null,
  onError = null,
} = {}) {
  const voiceBtn = document.getElementById(voiceBtnId);
  const voiceIcon = voiceIconId ? document.getElementById(voiceIconId) : null;
  const inputEl = inputId ? document.getElementById(inputId) : null;

  const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
  if (!voiceBtn) {
    console.warn('initVoice: voiceBtn not found for id', voiceBtnId);
  }

  const supported = Boolean(SpeechRecognition);
  let recognition = null;
  let listening = false;

  function updateUIListening(isListening) {
    if (!voiceBtn) return;
    listening = !!isListening;
    voiceBtn.classList.toggle('listening', listening);
    voiceBtn.setAttribute('aria-pressed', listening ? 'true' : 'false');
    if (voiceIcon) {
      voiceIcon.classList.toggle('fa-microphone', !listening);
      voiceIcon.classList.toggle('fa-microphone-slash', listening);
    }
  }

  function safeFillInput(text) {
    if (typeof onResult === 'function') {
      try {
        onResult(text);
      } catch (e) {
        console.error('onResult callback error', e);
      }
      return;
    }
    if (inputEl) inputEl.value = text;
  }

  function setupRecognition() {
    if (!supported) return null;
    if (recognition) return recognition;

    recognition = new SpeechRecognition();
    recognition.continuous = !!continuous;
    recognition.interimResults = !!interimResults;
    recognition.lang = lang;

    recognition.onstart = () => {
      updateUIListening(true);
      if (typeof onStart === 'function') onStart();
    };

    recognition.onresult = (evt) => {
      try {
        const resIndex = evt.results.length - 1;
        const transcript = evt.results[resIndex][0]?.transcript ?? '';
        safeFillInput(transcript, evt);
      } catch (e) {
        console.error('voice onresult error', e);
      }
    };

    recognition.onend = () => {
      updateUIListening(false);
      if (typeof onEnd === 'function') onEnd();
    };

    recognition.onerror = (evt) => {
      console.error('Voice recognition error', evt && evt.error);
      updateUIListening(false);
      if (typeof onError === 'function') onError(evt);
    };

    return recognition;
  }

  function start() {
    if (!supported) {
      console.warn('SpeechRecognition not supported in this browser');
      return;
    }
    setupRecognition();
    try {
      recognition.start();
    } catch (e) {
      console.warn('recognition.start() error', e);
    }
  }

  function stop() {
    if (!supported || !recognition) return;
    try {
      recognition.stop();
    } catch (e) {
      console.warn('recognition.stop() error', e);
    }
  }

  function toggle() {
    if (!supported) return;
    if (listening) stop();
    else start();
  }

  function destroy() {
    if (!recognition) return;
    try {
      recognition.onstart = null;
      recognition.onresult = null;
      recognition.onend = null;
      recognition.onerror = null;
      try {
        recognition.abort?.();
      } catch {}
    } catch (e) { }
    recognition = null;
    updateUIListening(false);
    if (voiceBtn) voiceBtn.removeEventListener('click', clickHandler);
  }

  function clickHandler(e) {
    e.preventDefault();
    toggle();
  }

  if (voiceBtn) {
    voiceBtn.style.display = supported || autoShowIfUnsupported ? 'block' : 'none';
    voiceBtn.addEventListener('click', clickHandler);
    voiceBtn.setAttribute('aria-pressed', 'false');
  }

  return {
    supported,
    start,
    stop,
    toggle,
    destroy,
    setLang(newLang) {
      lang = newLang;
      if (recognition) recognition.lang = newLang;
    },
    isListening: () => !!listening,
  };
}
