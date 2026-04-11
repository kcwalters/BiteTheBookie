(function () {
  var LEAGUE_KEY = 'btb-active-league';
  var leagues = ['nfl', 'nba', 'nhl', 'ncaa', 'mlb'];

  function showTicker(league) {
    leagues.forEach(function (l) {
      var el = document.getElementById(l + '-ticker');
      if (el) el.style.display = (l === league) ? '' : 'none';

      var cb = document.getElementById('cb-' + l);
      if (cb) cb.checked = (l === league);
    });

    var details = document.getElementById('ticker-controls-details');
    if (details) details.open = true;

    try { localStorage.setItem(LEAGUE_KEY, league); } catch (e) {}
  }

  function initLeagueMenuTickerSwitcher() {
    var container = document.querySelector('.league-menu .container');
    if (!container) return;

    // Restore last selection — default to 'nfl'
    var saved = null;
    try { saved = localStorage.getItem(LEAGUE_KEY); } catch (e) {}
    showTicker(saved && leagues.indexOf(saved) !== -1 ? saved : 'nfl');

    container.addEventListener('click', function (e) {
      var link = e.target.closest('a[data-league]');
      if (!link) return;
      e.preventDefault();
      var league = link.getAttribute('data-league');
      if (league) showTicker(league);
    });
  }

  document.addEventListener('DOMContentLoaded', initLeagueMenuTickerSwitcher);
})();

