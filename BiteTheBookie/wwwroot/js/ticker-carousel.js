(function () {
  function initTickerCarousel(root) {
    const viewport = root.querySelector('.ticker-viewport');
    if (!viewport) return;

    const slides = Array.from(viewport.querySelectorAll('.ticker-slide'));
    const prev = root.querySelector('.ticker-prev');
    const next = root.querySelector('.ticker-next');

    // If slides exist, keep the existing page-based behavior.
    if (slides.length > 0) {
      function getActiveIndex() {
        let idx = slides.findIndex(s => s.getAttribute('data-active') === 'true');
        if (idx < 0) idx = 0;
        return idx;
      }

      function setActive(index) {
        const normalized = ((index % slides.length) + slides.length) % slides.length;
        slides.forEach((s, i) => {
          if (i === normalized) s.setAttribute('data-active', 'true');
          else s.removeAttribute('data-active');
        });
      }

      prev?.addEventListener('click', function () {
        setActive(getActiveIndex() - 1);
      });

      next?.addEventListener('click', function () {
        setActive(getActiveIndex() + 1);
      });

      root.addEventListener('keydown', function (e) {
        if (e.key === 'ArrowLeft') {
          setActive(getActiveIndex() - 1);
        } else if (e.key === 'ArrowRight') {
          setActive(getActiveIndex() + 1);
        }
      });

      return;
    }

    // Otherwise, treat this as a horizontal scroller: move the ticker track left/right.
    const track = viewport.querySelector('.nfl-ticker-track, .nba-ticker-track, .nhl-ticker-track');
    if (!track) return;

    let offset = 0;

    function getMaxOffset() {
      const viewportWidth = viewport.clientWidth;
      const trackWidth = track.scrollWidth;
      return Math.max(0, trackWidth - viewportWidth);
    }

    function applyOffset() {
      const max = getMaxOffset();
      if (offset < 0) offset = 0;
      if (offset > max) offset = max;
      track.style.transform = `translateX(${-offset}px)`;
    }

    function step() {
      return Math.max(120, Math.floor(viewport.clientWidth * 0.6));
    }

    prev?.addEventListener('click', function () {
      offset -= step();
      applyOffset();
    });

    next?.addEventListener('click', function () {
      offset += step();
      applyOffset();
    });

    root.addEventListener('keydown', function (e) {
      if (e.key === 'ArrowLeft') {
        offset -= step();
        applyOffset();
      } else if (e.key === 'ArrowRight') {
        offset += step();
        applyOffset();
      }
    });

    window.addEventListener('resize', applyOffset);

    applyOffset();
  }

  document.addEventListener('DOMContentLoaded', function () {
    document
      .querySelectorAll('.nfl-ticker-carousel, .nba-ticker-carousel, .nhl-ticker-carousel, .ncaa-ticker-carousel')
      .forEach(initTickerCarousel);
  });
})();
