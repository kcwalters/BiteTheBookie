(function () {
  var LEAGUE_KEY = 'btb-active-league';
  var leagues = ['nfl', 'nba', 'nhl', 'ncaa', 'ncaaf', 'mlb'];

  function showTicker(league) {
    leagues.forEach(function (l) {
      var el = document.getElementById(l + '-ticker');
      if (el) {
        var active = (l === league);
        el.classList.toggle('ticker-active', active);
        el.style.display = active ? 'block' : 'none';
        el.setAttribute('aria-hidden', active ? 'false' : 'true');
      }
    });

    var select = document.getElementById('tickerLeagueSelect');
    if (select && select.value !== league) select.value = league;
    updateLeagueLogo(league);

    try { localStorage.setItem(LEAGUE_KEY, league); } catch (e) {}
  }

  // Swap the logo shown above the dropdown to match the active league.
  function updateLeagueLogo(league) {
    var logo = document.getElementById('tickerLeagueLogo');
    var select = document.getElementById('tickerLeagueSelect');
    if (!logo || !select) return;
    var opt = select.querySelector('option[value="' + league + '"]');
    if (opt && opt.getAttribute('data-logo')) {
      logo.src = opt.getAttribute('data-logo');
      logo.alt = (opt.getAttribute('data-label') || opt.textContent) + ' logo';
    }
  }

  function tickerHasGames(league) {
    var el = document.getElementById(league + '-ticker');
    return !!(el && el.querySelector('.cbb-game'));
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
    var select = document.getElementById('tickerLeagueSelect');
    if (!select) return;

    // Disable (but keep visible) any league option that has no games so users
    // can see the full list yet can't land on an empty, out-of-season board.
    Array.prototype.forEach.call(select.options, function (opt) {
      var hasGames = tickerHasGames(opt.value);
      opt.disabled = !hasGames;
      var baseLabel = opt.getAttribute('data-label') || opt.textContent.replace(/\s*\(out of season\)\s*$/i, '');
      opt.setAttribute('data-label', baseLabel);
      opt.textContent = hasGames ? baseLabel : baseLabel + ' (out of season)';
    });

    // Restore last selection, but only if that league currently has games; otherwise
    // default to the first in-season league so users never land on an empty ticker.
    var saved = null;
    try { saved = localStorage.getItem(LEAGUE_KEY); } catch (e) {}
    showTicker(pickInitialLeague(saved));

    select.addEventListener('change', function () {
      showTicker(select.value);
    });
  }

  document.addEventListener('DOMContentLoaded', initLeagueMenuTickerSwitcher);
})();

