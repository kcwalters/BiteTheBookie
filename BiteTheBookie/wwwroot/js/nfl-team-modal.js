(function () {
  const nflTeams = [
    { abbr: 'ARI', name: 'Arizona Cardinals' },
    { abbr: 'ATL', name: 'Atlanta Falcons' },
    { abbr: 'BAL', name: 'Baltimore Ravens' },
    { abbr: 'BUF', name: 'Buffalo Bills' },
    { abbr: 'CAR', name: 'Carolina Panthers' },
    { abbr: 'CHI', name: 'Chicago Bears' },
    { abbr: 'CIN', name: 'Cincinnati Bengals' },
    { abbr: 'CLE', name: 'Cleveland Browns' },
    { abbr: 'DAL', name: 'Dallas Cowboys' },
    { abbr: 'DEN', name: 'Denver Broncos' },
    { abbr: 'DET', name: 'Detroit Lions' },
    { abbr: 'GB', name: 'Green Bay Packers' },
    { abbr: 'HOU', name: 'Houston Texans' },
    { abbr: 'IND', name: 'Indianapolis Colts' },
    { abbr: 'JAX', name: 'Jacksonville Jaguars' },
    { abbr: 'KC', name: 'Kansas City Chiefs' },
    { abbr: 'LV', name: 'Las Vegas Raiders' },
    { abbr: 'LAC', name: 'Los Angeles Chargers' },
    { abbr: 'LAR', name: 'Los Angeles Rams' },
    { abbr: 'MIA', name: 'Miami Dolphins' },
    { abbr: 'MIN', name: 'Minnesota Vikings' },
    { abbr: 'NE', name: 'New England Patriots' },
    { abbr: 'NO', name: 'New Orleans Saints' },
    { abbr: 'NYG', name: 'New York Giants' },
    { abbr: 'NYJ', name: 'New York Jets' },
    { abbr: 'PHI', name: 'Philadelphia Eagles' },
    { abbr: 'PIT', name: 'Pittsburgh Steelers' },
    { abbr: 'SF', name: 'San Francisco 49ers' },
    { abbr: 'SEA', name: 'Seattle Seahawks' },
    { abbr: 'TB', name: 'Tampa Bay Buccaneers' },
    { abbr: 'TEN', name: 'Tennessee Titans' },
    { abbr: 'WAS', name: 'Washington Commanders' }
  ];

  const nbaTeams = [
    { abbr: 'ATL', name: 'Atlanta Hawks' },
    { abbr: 'BOS', name: 'Boston Celtics' },
    { abbr: 'BKN', name: 'Brooklyn Nets' },
    { abbr: 'CHA', name: 'Charlotte Hornets' },
    { abbr: 'CHI', name: 'Chicago Bulls' },
    { abbr: 'CLE', name: 'Cleveland Cavaliers' },
    { abbr: 'DAL', name: 'Dallas Mavericks' },
    { abbr: 'DEN', name: 'Denver Nuggets' },
    { abbr: 'DET', name: 'Detroit Pistons' },
    { abbr: 'GSW', name: 'Golden State Warriors' },
    { abbr: 'HOU', name: 'Houston Rockets' },
    { abbr: 'IND', name: 'Indiana Pacers' },
    { abbr: 'LAC', name: 'LA Clippers' },
    { abbr: 'LAL', name: 'Los Angeles Lakers' },
    { abbr: 'MEM', name: 'Memphis Grizzlies' },
    { abbr: 'MIA', name: 'Miami Heat' },
    { abbr: 'MIL', name: 'Milwaukee Bucks' },
    { abbr: 'MIN', name: 'Minnesota Timberwolves' },
    { abbr: 'NOP', name: 'New Orleans Pelicans' },
    { abbr: 'NYK', name: 'New York Knicks' },
    { abbr: 'OKC', name: 'Oklahoma City Thunder' },
    { abbr: 'ORL', name: 'Orlando Magic' },
    { abbr: 'PHI', name: 'Philadelphia 76ers' },
    { abbr: 'PHX', name: 'Phoenix Suns' },
    { abbr: 'POR', name: 'Portland Trail Blazers' },
    { abbr: 'SAC', name: 'Sacramento Kings' },
    { abbr: 'SAS', name: 'San Antonio Spurs' },
    { abbr: 'TOR', name: 'Toronto Raptors' },
    { abbr: 'UTA', name: 'Utah Jazz' },
    { abbr: 'WAS', name: 'Washington Wizards' }
  ];

  // Public, stable CDN for team logos (no copyrighted ESPN assets in repo)
  // Source: https://github.com/StevenDaily/nfl-football-logos (raw GitHub CDN)
  function nflLogoUrl(abbr) {
    return `https://raw.githubusercontent.com/StevenDaily/nfl-football-logos/master/svg/${abbr}.svg`;
  }

  function nbaLogoUrl(abbr) {
    const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="36" height="36"><rect width="36" height="36" rx="6" fill="#1f2937"/><text x="50%" y="50%" dominant-baseline="middle" text-anchor="middle" fill="#ffffff" font-family="Arial, sans-serif" font-size="12" font-weight="700">${abbr}</text></svg>`;
    return `data:image/svg+xml;utf8,${encodeURIComponent(svg)}`;
  }

  function buildModalBodyHtml(columns, teamMap, logoUrl) {
    return columns
      .map(col => {
        const items = col.teams
          .map(abbr => {
            const t = teamMap.get(abbr);
            if (!t) return '';
            return `
              <a class="nfl-team-modal__team" href="#" data-team="${abbr}">
                <img class="nfl-team-modal__logo" src="${logoUrl(abbr)}" alt="${t.name}" loading="lazy" />
                <span class="nfl-team-modal__name">${t.name}</span>
              </a>`;
          })
          .join('');

        return `
          <div class="nfl-team-modal__col">
            <div class="nfl-team-modal__col-title">${col.title}</div>
            <div class="nfl-team-modal__teams">
              ${items}
            </div>
          </div>`;
      })
      .join('');
  }

  const nflColumns = [
    { title: 'AFC East', teams: ['BUF', 'MIA', 'NE', 'NYJ'] },
    { title: 'AFC North', teams: ['BAL', 'CIN', 'CLE', 'PIT'] },
    { title: 'AFC South', teams: ['HOU', 'IND', 'JAX', 'TEN'] },
    { title: 'AFC West', teams: ['DEN', 'KC', 'LAC', 'LV'] },
    { title: 'NFC East', teams: ['DAL', 'NYG', 'PHI', 'WAS'] },
    { title: 'NFC North', teams: ['CHI', 'DET', 'GB', 'MIN'] },
    { title: 'NFC South', teams: ['ATL', 'CAR', 'NO', 'TB'] },
    { title: 'NFC West', teams: ['ARI', 'LAR', 'SEA', 'SF'] }
  ];

  const nbaColumns = [
    { title: 'Atlantic', teams: ['BOS', 'BKN', 'NYK', 'PHI', 'TOR'] },
    { title: 'Central', teams: ['CHI', 'CLE', 'DET', 'IND', 'MIL'] },
    { title: 'Southeast', teams: ['ATL', 'CHA', 'MIA', 'ORL', 'WAS'] },
    { title: 'Northwest', teams: ['DEN', 'MIN', 'OKC', 'POR', 'UTA'] },
    { title: 'Pacific', teams: ['GSW', 'LAC', 'LAL', 'PHX', 'SAC'] },
    { title: 'Southwest', teams: ['DAL', 'HOU', 'MEM', 'NOP', 'SAS'] }
  ];

  function showTicker(league) {
    const tickers = ['nfl', 'nba', 'nhl', 'ncaa'];
    tickers.forEach(item => {
      const ticker = document.getElementById(item + '-ticker');
      if (ticker) {
        ticker.style.display = (item === league) ? '' : 'none';
      }
    });

    try {
      localStorage.setItem('selectedSportsTicker', league);
    } catch {}

    document.dispatchEvent(new CustomEvent('sportsTickerSelected', { detail: { league } }));
  }

  function initLeagueTeamModal(options) {
    const link = document.querySelector(`.league-menu a[data-league="${options.league}"]`);
    const modalEl = document.getElementById(options.modalId);
    const modalBody = modalEl?.querySelector('.modal-body');

    if (!link || !modalEl || !modalBody || !window.bootstrap?.Modal) return;

    const teamMap = new Map(options.teams.map(t => [t.abbr, t]));
    modalBody.innerHTML = `<div class="nfl-team-modal__grid">${buildModalBodyHtml(options.columns, teamMap, options.logoUrl)}</div>`;

    const modal = bootstrap.Modal.getOrCreateInstance(modalEl, {
      backdrop: true,
      focus: false
    });

    let hideTimer = null;
    function clearHideTimer() {
      if (hideTimer) {
        clearTimeout(hideTimer);
        hideTimer = null;
      }
    }

    function scheduleHide() {
      clearHideTimer();
      hideTimer = setTimeout(() => {
        modal.hide();
      }, 0);
    }

    function show() {
      clearHideTimer();
      modal.show();
      showTicker(options.league);
    }

    link.addEventListener('mouseenter', show);
    link.addEventListener('focus', show);
    link.addEventListener('mouseleave', scheduleHide);

    link.addEventListener('click', (e) => {
      e.preventDefault();
      show();
    });

    modalEl.addEventListener('mouseleave', () => {
      modal.hide();
    });

    modalEl.addEventListener('click', (e) => {
      const team = e.target.closest('.nfl-team-modal__team');
      if (!team) return;
      e.preventDefault();
      modal.hide();
    });

    modalEl.addEventListener('hidden.bs.modal', clearHideTimer);
  }

  document.addEventListener('DOMContentLoaded', function () {
    initLeagueTeamModal({
      league: 'nfl',
      modalId: 'nflTeamModal',
      teams: nflTeams,
      columns: nflColumns,
      logoUrl: nflLogoUrl
    });

    initLeagueTeamModal({
      league: 'nba',
      modalId: 'nbaTeamModal',
      teams: nbaTeams,
      columns: nbaColumns,
      logoUrl: nbaLogoUrl
    });
  });

  navigator.permissions.query({ name: "geolocation" })
  // or, if storing:
  const query = navigator.permissions.query.bind(navigator.permissions);
  query({ name: "geolocation" });
})();
 



