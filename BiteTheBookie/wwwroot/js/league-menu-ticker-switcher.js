(function () {
  var LEAGUE_KEY = 'btb-active-league';
  var COOKIE_MAX_AGE = 60 * 60 * 24 * 365; // 1 year in seconds
  var leagues = ['nfl', 'nba', 'nhl', 'ncaa', 'ncaaf', 'mlb'];

  // Persist the active league in a cookie so it survives across browser sessions.
  function setLeagueCookie(league) {
    try {
      document.cookie = LEAGUE_KEY + '=' + encodeURIComponent(league) +
        ';path=/;max-age=' + COOKIE_MAX_AGE + ';SameSite=Lax';
    } catch (e) {}
  }

  function getLeagueCookie() {
    try {
      var match = document.cookie.match(new RegExp('(?:^|;\\s*)' + LEAGUE_KEY + '=([^;]*)'));
      return match ? decodeURIComponent(match[1]) : null;
    } catch (e) {
      return null;
    }
  }

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
    setLeagueCookie(league);
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
    // Prefer the cookie (shared across sessions), falling back to localStorage.
    var saved = getLeagueCookie();
    if (!saved) {
      try { saved = localStorage.getItem(LEAGUE_KEY); } catch (e) {}
    }
    showTicker(pickInitialLeague(saved));

    select.addEventListener('change', function () {
      showTicker(select.value);
    });
  }

  document.addEventListener('DOMContentLoaded', initLeagueMenuTickerSwitcher);
})();

