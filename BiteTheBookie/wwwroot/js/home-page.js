(function () {
  function initLogoLightbox() {
    const trigger = document.getElementById('siteLogo');
    const modal = document.getElementById('imageModal');
    const modalImg = document.getElementById('imageModalImg');
    const closeBtn = document.getElementById('imageModalClose');
    if (!trigger || !modal || !modalImg || !closeBtn) return;

    function openModal(src, alt) {
      modalImg.src = src;
      modalImg.alt = alt || '';
      modal.classList.add('open');
    }

    function closeModal() {
      modal.classList.remove('open');
      modalImg.src = '';
    }

    trigger.addEventListener('click', function (e) {
      e.preventDefault();
      openModal(trigger.getAttribute('src') || 'img/slider-1.png', trigger.getAttribute('alt') || '');
    });

    closeBtn.addEventListener('click', function () { closeModal(); });
    modal.addEventListener('click', function (e) { if (e.target === modal) closeModal(); });
    document.addEventListener('keydown', function (e) { if (e.key === 'Escape') closeModal(); });
  }

  function initHomeTickerVisibility() {
    const STORAGE_KEY = 'selectedSportsTicker';
    const DEFAULT = 'nfl';

    function normalize(v) {
      v = (v || '').toString().toLowerCase();
      return (v === 'nfl' || v === 'nba' || v === 'nhl' || v === 'ncaa' || v === 'mlb') ? v : DEFAULT;
    }

    function showTicker(league) {
      const nfl = document.getElementById('nfl-ticker');
      const nba = document.getElementById('nba-ticker');
      const nhl = document.getElementById('nhl-ticker');
      const ncaa = document.getElementById('ncaa-ticker');
      const mlb = document.getElementById('mlb-ticker');
      if (!nfl || !nba || !nhl || !ncaa || !mlb) return;

      nfl.style.display = league === 'nfl' ? '' : 'none';
      nba.style.display = league === 'nba' ? '' : 'none';
      nhl.style.display = league === 'nhl' ? '' : 'none';
      ncaa.style.display = league === 'ncaa' ? '' : 'none';
      mlb.style.display = league === 'mlb' ? '' : 'none';
    }

    function applySelection(league) {
      const normalized = normalize(league);
      showTicker(normalized);
      try { localStorage.setItem(STORAGE_KEY, normalized); } catch { }
    }

    let saved = DEFAULT;
    try { saved = normalize(localStorage.getItem(STORAGE_KEY)); } catch { saved = DEFAULT; }
    showTicker(saved);

    document.addEventListener('sportsTickerSelected', function (e) {
      applySelection(e?.detail?.league);
    });

    document.addEventListener('ticker:select', function (e) {
      applySelection(e?.detail?.league);
    });

    document.addEventListener('tickerSelected', function (e) {
      applySelection(e?.detail?.league);
    });

    document.querySelectorAll('[data-league]')
      .forEach(function (el) {
        el.addEventListener('click', function () {
          applySelection(el.getAttribute('data-league'));
        });
      });
  }

  document.addEventListener('DOMContentLoaded', function () {
    initLogoLightbox();
    initHomeTickerVisibility();
  });
})();
