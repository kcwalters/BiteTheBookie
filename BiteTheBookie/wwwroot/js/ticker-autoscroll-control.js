(function () {
  const STORAGE_KEY = 'tickersAutoScroll';

  function getPreference() {
    try {
      const v = localStorage.getItem(STORAGE_KEY);
      if (v === null) return false; // default: do NOT auto scroll
      return v === '1' || v === 'true';
    } catch {
      return false;
    }
  }

  function applyPreference() {
    const enabled = getPreference();
    document.documentElement.classList.toggle('tickers-autoscroll', enabled);
    document.documentElement.classList.toggle('tickers-noautoscroll', !enabled);
  }

  document.addEventListener('DOMContentLoaded', applyPreference);
  window.setTickersAutoScroll = function (enabled) {
    try {
      localStorage.setItem(STORAGE_KEY, enabled ? '1' : '0');
    } catch {}
    applyPreference();
  };
})();
