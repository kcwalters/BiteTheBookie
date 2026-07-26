(function () {
  var LEAGUE_KEY = 'btb-active-league';
  var leagues = ['nfl', 'nba', 'nhl', 'ncaa', 'ncaaf', 'mlb'];

  function showTicker(league) {
    // Hide every ticker in the row first so only one can ever be visible at a time.
    var row = document.querySelector('.tickers-row');
    if (row) {
      Array.prototype.forEach.call(row.children, function (child) {
        child.classList.remove('ticker-active');
        child.style.display = 'none';
        child.setAttribute('aria-hidden', 'true');
      });
    }

    leagues.forEach(function (l) {
      var el = document.getElementById(l + '-ticker');
      if (el) {
        var active = (l === league);
        el.classList.toggle('ticker-active', active);
        el.style.display = active ? 'block' : 'none';
        el.setAttribute('aria-hidden', active ? 'false' : 'true');
      }

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

    // Switch the ticker when a league menu item is hovered over.
    container.addEventListener('mouseover', function (e) {
      var link = e.target.closest('a[data-league]');
      if (!link) return;
      var league = link.getAttribute('data-league');
      if (league) showTicker(league);
    });
  }

  document.addEventListener('DOMContentLoaded', initLeagueMenuTickerSwitcher);
})();

