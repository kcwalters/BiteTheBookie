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

  const cbbTeams = [
    // ACC
    { abbr: 'CLEM', name: 'Clemson' },
    { abbr: 'DUKE', name: 'Duke' },
    { abbr: 'UNC', name: 'North Carolina' },
    { abbr: 'NCSU', name: 'NC State' },
    { abbr: 'UVA', name: 'Virginia' },
    { abbr: 'VT', name: 'Virginia Tech' },
    { abbr: 'WAKE', name: 'Wake Forest' },
    { abbr: 'MIA', name: 'Miami' },
    { abbr: 'FSU', name: 'Florida State' },
    { abbr: 'LOU', name: 'Louisville' },
    { abbr: 'PITT', name: 'Pittsburgh' },
    { abbr: 'SYR', name: 'Syracuse' },
    { abbr: 'BC', name: 'Boston College' },
    { abbr: 'GT', name: 'Georgia Tech' },
    { abbr: 'ND', name: 'Notre Dame' },
    // Big Ten
    { abbr: 'ILL', name: 'Illinois' },
    { abbr: 'IND', name: 'Indiana' },
    { abbr: 'IOWA', name: 'Iowa' },
    { abbr: 'MD', name: 'Maryland' },
    { abbr: 'MICH', name: 'Michigan' },
    { abbr: 'MSU', name: 'Michigan State' },
    { abbr: 'MINN', name: 'Minnesota' },
    { abbr: 'NEB', name: 'Nebraska' },
    { abbr: 'NW', name: 'Northwestern' },
    { abbr: 'OSU', name: 'Ohio State' },
    { abbr: 'PSU', name: 'Penn State' },
    { abbr: 'PUR', name: 'Purdue' },
    { abbr: 'RUT', name: 'Rutgers' },
    { abbr: 'WIS', name: 'Wisconsin' },
    // Big 12
    { abbr: 'BAY', name: 'Baylor' },
    { abbr: 'ISU', name: 'Iowa State' },
    { abbr: 'KU', name: 'Kansas' },
    { abbr: 'KSU', name: 'Kansas State' },
    { abbr: 'OU', name: 'Oklahoma' },
    { abbr: 'OST', name: 'Oklahoma State' },
    { abbr: 'TCU', name: 'TCU' },
    { abbr: 'TEX', name: 'Texas' },
    { abbr: 'TTU', name: 'Texas Tech' },
    { abbr: 'WVU', name: 'West Virginia' },
    // SEC
    { abbr: 'ALA', name: 'Alabama' },
    { abbr: 'ARK', name: 'Arkansas' },
    { abbr: 'AUB', name: 'Auburn' },
    { abbr: 'FLA', name: 'Florida' },
    { abbr: 'UGA', name: 'Georgia' },
    { abbr: 'UK', name: 'Kentucky' },
    { abbr: 'LSU', name: 'LSU' },
    { abbr: 'MISS', name: 'Ole Miss' },
    { abbr: 'MST', name: 'Mississippi State' },
    { abbr: 'USC', name: 'South Carolina' },
    { abbr: 'TENN', name: 'Tennessee' },
    { abbr: 'TAMU', name: 'Texas A&M' },
    { abbr: 'VAN', name: 'Vanderbilt' },
    // Pac-12
    { abbr: 'ARIZ', name: 'Arizona' },
    { abbr: 'ASU', name: 'Arizona State' },
    { abbr: 'CAL', name: 'California' },
    { abbr: 'COLO', name: 'Colorado' },
    { abbr: 'ORE', name: 'Oregon' },
    { abbr: 'ORST', name: 'Oregon State' },
    { abbr: 'STAN', name: 'Stanford' },
    { abbr: 'UCLA', name: 'UCLA' },
    { abbr: 'WASH', name: 'Washington' },
    { abbr: 'WSU', name: 'Washington State' },
    // Big East
    { abbr: 'BUT', name: 'Butler' },
    { abbr: 'CRE', name: 'Creighton' },
    { abbr: 'DPU', name: 'DePaul' },
    { abbr: 'GTWN', name: 'Georgetown' },
    { abbr: 'MARQ', name: 'Marquette' },
    { abbr: 'PROV', name: 'Providence' },
    { abbr: 'SHU', name: 'Seton Hall' },
    { abbr: 'SJU', name: 'St. John\'s' },
    { abbr: 'VILL', name: 'Villanova' },
    { abbr: 'XAV', name: 'Xavier' }
  ];

  function cbbLogoUrl(abbr) {
    const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="36" height="36"><rect width="36" height="36" rx="6" fill="#003366"/><text x="50%" y="50%" dominant-baseline="middle" text-anchor="middle" fill="#ffffff" font-family="Arial, sans-serif" font-size="10" font-weight="700">${abbr}</text></svg>`;
    return `data:image/svg+xml;utf8,${encodeURIComponent(svg)}`;
  }

  // ESPN team ID mappings for major college basketball teams
  const espnTeamIds = {
    'DUKE': '150', 'UNC': '153', 'UVA': '258', 'CLEM': '228', 'NCSU': '152',
    'WAKE': '154', 'VT': '259', 'MIA': '2390', 'FSU': '52', 'LOU': '97',
    'PITT': '221', 'SYR': '183', 'BC': '103', 'GT': '59', 'ND': '87',
    'ILL': '356', 'IND': '84', 'IOWA': '2294', 'MD': '120', 'MICH': '130',
    'MSU': '127', 'MINN': '135', 'NEB': '158', 'NW': '77', 'OSU': '194',
    'PSU': '213', 'PUR': '2509', 'RUT': '164', 'WIS': '275',
    'BAY': '239', 'ISU': '66', 'KU': '2305', 'KSU': '2306', 'OU': '201',
    'OST': '197', 'TCU': '2628', 'TEX': '251', 'TTU': '2641', 'WVU': '277',
    'ALA': '333', 'ARK': '8', 'AUB': '2', 'FLA': '57', 'UGA': '61',
    'UK': '96', 'LSU': '99', 'MISS': '145', 'MST': '344', 'USC': '2579',
    'TENN': '2633', 'TAMU': '245', 'VAN': '238',
    'ARIZ': '12', 'ASU': '9', 'CAL': '25', 'COLO': '38', 'ORE': '2483',
    'ORST': '204', 'STAN': '24', 'UCLA': '26', 'WASH': '264', 'WSU': '265',
    'BUT': '2086', 'CRE': '156', 'DPU': '305', 'GTWN': '46', 'MARQ': '269',
    'PROV': '2507', 'SHU': '2550', 'SJU': '2599', 'VILL': '222', 'XAV': '2752'
  };

  function getEspnTeamUrl(abbr, teamName) {
    const teamId = espnTeamIds[abbr];
    if (teamId) {
      const urlName = teamName.toLowerCase().replace(/['\s]/g, '-').replace(/&/g, '');
      return `https://www.espn.com/mens-college-basketball/team/_/id/${teamId}/${urlName}`;
    }
    return 'https://www.espn.com/mens-college-basketball/teams';
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

  function buildCBBModalBodyHtml(columns, teamMap, logoUrl) {
    return columns
      .map(col => {
        const items = col.teams
          .map(abbr => {
            const t = teamMap.get(abbr);
            if (!t) return '';
            const espnUrl = getEspnTeamUrl(abbr, t.name);
            return `
              <a class="nfl-team-modal__team" href="${espnUrl}" target="_blank" data-team="${abbr}">
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

  const cbbColumns = [
    { title: 'ACC', teams: ['DUKE', 'UNC', 'UVA', 'CLEM', 'NCSU', 'WAKE', 'VT', 'MIA', 'FSU', 'LOU', 'PITT', 'SYR', 'BC', 'GT', 'ND'] },
    { title: 'Big Ten', teams: ['ILL', 'IND', 'IOWA', 'MD', 'MICH', 'MSU', 'MINN', 'NEB', 'NW', 'OSU', 'PSU', 'PUR', 'RUT', 'WIS'] },
    { title: 'Big 12', teams: ['BAY', 'ISU', 'KU', 'KSU', 'OU', 'OST', 'TCU', 'TEX', 'TTU', 'WVU'] },
    { title: 'SEC', teams: ['ALA', 'ARK', 'AUB', 'FLA', 'UGA', 'UK', 'LSU', 'MISS', 'MST', 'USC', 'TENN', 'TAMU', 'VAN'] },
    { title: 'Pac-12', teams: ['ARIZ', 'ASU', 'CAL', 'COLO', 'ORE', 'ORST', 'STAN', 'UCLA', 'WASH', 'WSU'] },
    { title: 'Big East', teams: ['BUT', 'CRE', 'DPU', 'GTWN', 'MARQ', 'PROV', 'SHU', 'SJU', 'VILL', 'XAV'] }
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
    const buildFn = options.buildFunction || buildModalBodyHtml;
    const oddsLinkHtml = options.oddsLink ? '<div class="nfl-team-modal__actions"><a class="nfl-team-modal__odds-link btn btn-primary btn-sm mb-3" href="' + options.oddsLink.url + '">' + options.oddsLink.label + '</a></div>' : '';
    modalBody.innerHTML = oddsLinkHtml + '<div class="nfl-team-modal__grid">' + buildFn(options.columns, teamMap, options.logoUrl) + '</div>';;

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
      
      // Check if this is a real link (not "#") - if so, let it open
      const href = team.getAttribute('href');
      if (href && href !== '#') {
        // Let the link open in new tab, then hide modal
        modal.hide();
        return;
      }
      
      // For placeholder links (#), prevent default and just close
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
      logoUrl: nflLogoUrl,
      oddsLink: { url: '/Odds/NFL', label: 'View NFL Odds' }
    });

    initLeagueTeamModal({
      league: 'nba',
      modalId: 'nbaTeamModal',
      teams: nbaTeams,
      columns: nbaColumns,
      logoUrl: nbaLogoUrl,
      oddsLink: { url: '/Odds/NBA', label: 'View NBA Odds' }
    });

    initLeagueTeamModal({
      league: 'ncaa',
      modalId: 'cbbTeamModal',
      teams: cbbTeams,
      columns: cbbColumns,
      logoUrl: cbbLogoUrl,
      buildFunction: buildCBBModalBodyHtml,
      oddsLink: { url: '/Odds/CBB', label: 'View CBB Odds' }
    });
  });

  navigator.permissions.query({ name: "geolocation" })
  // or, if storing:
  const query = navigator.permissions.query.bind(navigator.permissions);
  query({ name: "geolocation" });
})();
 






