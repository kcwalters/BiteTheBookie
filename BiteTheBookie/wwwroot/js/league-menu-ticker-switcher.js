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

  function tickerHasGames(league) {
    var el = document.getElementById(league + '-ticker');
    return !!(el && el.querySelector('.nfl-ticker-item'));
  }

  // Picks the league to show on load: the previously selected one (if it still has
  // games), otherwise the first league in preferred order that actually has games
  // today/upcoming, falling back to the first league overall.
  function pickInitialLeague(saved) {
    if (saved && leagues.indexOf(saved) !== -1 && tickerHasGames(saved)) {
      return saved;
    }
    for (var i = 0; i < leagues.length; i++) {
      if (tickerHasGames(leagues[i])) {
        return leagues[i];
      }
    }
    return leagues[0];
  }

  function initLeagueMenuTickerSwitcher() {
    var container = document.querySelector('.league-menu .container');
    if (!container) return;

    // Restore last selection, but only if that league currently has games; otherwise
    // default to the first in-season league so users never land on an empty ticker.
    var saved = null;
    try { saved = localStorage.getItem(LEAGUE_KEY); } catch (e) {}
    showTicker(pickInitialLeague(saved));

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

