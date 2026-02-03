(function () {
  function initLeagueMenuTickerSwitcher() {
    const container = document.querySelector('.league-menu .container');
    if (!container) return;

    const leagues = ['nfl', 'nba', 'nhl', 'ncaa'];

    function showTicker(league) {
      leagues.forEach(l => {
        const el = document.getElementById(l + '-ticker');
        if (el) el.style.display = (l === league) ? '' : 'none';

        const cb = document.getElementById('cb-' + l);
        if (cb) cb.checked = (l === league);
      });

      const details = document.getElementById('ticker-controls-details');
      if (details) details.open = true;
    }

    container.addEventListener('click', function (e) {
      const link = e.target.closest('a[data-league]');
      if (!link) return;
      e.preventDefault();
      const league = link.getAttribute('data-league');
      if (league) showTicker(league);
    });
  }

  document.addEventListener('DOMContentLoaded', initLeagueMenuTickerSwitcher);
})();
