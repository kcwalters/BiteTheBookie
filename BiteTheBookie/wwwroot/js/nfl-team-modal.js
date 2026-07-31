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

  // Real team logos from ESPN's public CDN (lowercase abbreviation).
  function nflLogoUrl(abbr) {
    const espnCode = {
      WAS: 'wsh' // Washington Commanders
    }[abbr] || abbr.toLowerCase();
    return `https://a.espncdn.com/i/teamlogos/nfl/500/${espnCode}.png`;
  }

  function nbaLogoUrl(abbr) {
    // Prefer ESPN CDN NBA logos; fallback to SVG tile if unavailable.
    try {
      const code = (abbr || '').toLowerCase();
      return `https://a.espncdn.com/i/teamlogos/nba/500/${code}.png`;
    } catch (e) {
      const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="36" height="36"><rect width="36" height="36" rx="6" fill="#1f2937"/><text x="50%" y="50%" dominant-baseline="middle" text-anchor="middle" fill="#ffffff" font-family="Arial, sans-serif" font-size="12" font-weight="700">${abbr}</text></svg>`;
      return `data:image/svg+xml;utf8,${encodeURIComponent(svg)}`;
    }
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
    // Reuse the shared ESPN school logos from the CFB list (same schools, same
    // logoId). Match by team name so College Basketball shows real logos like CFB.
    const cbb = cbbTeams.find(x => x.abbr === abbr);
    if (cbb) {
      const cfb = cfbTeams.find(x => x.name === cbb.name);
      if (cfb && cfb.logoId) {
        return `https://a.espncdn.com/i/teamlogos/ncaa/500/${cfb.logoId}.png`;
      }
    }
    const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="36" height="36"><rect width="36" height="36" rx="6" fill="#003366"/><text x="50%" y="50%" dominant-baseline="middle" text-anchor="middle" fill="#ffffff" font-family="Arial, sans-serif" font-size="10" font-weight="700">${abbr}</text></svg>`;
    return `data:image/svg+xml;utf8,${encodeURIComponent(svg)}`;
  }

  const mlbTeams = [
    // American League East
    { abbr: 'BAL', name: 'Baltimore Orioles' },
    { abbr: 'BOS', name: 'Boston Red Sox' },
    { abbr: 'NYY', name: 'New York Yankees' },
    { abbr: 'TB', name: 'Tampa Bay Rays' },
    { abbr: 'TOR', name: 'Toronto Blue Jays' },
    // American League Central
    { abbr: 'CWS', name: 'Chicago White Sox' },
    { abbr: 'CLE', name: 'Cleveland Guardians' },
    { abbr: 'DET', name: 'Detroit Tigers' },
    { abbr: 'KC', name: 'Kansas City Royals' },
    { abbr: 'MIN', name: 'Minnesota Twins' },
    // American League West
    { abbr: 'HOU', name: 'Houston Astros' },
    { abbr: 'LAA', name: 'Los Angeles Angels' },
    { abbr: 'OAK', name: 'Oakland Athletics' },
    { abbr: 'SEA', name: 'Seattle Mariners' },
    { abbr: 'TEX', name: 'Texas Rangers' },
    // National League East
    { abbr: 'ATL', name: 'Atlanta Braves' },
    { abbr: 'MIA', name: 'Miami Marlins' },
    { abbr: 'NYM', name: 'New York Mets' },
    { abbr: 'PHI', name: 'Philadelphia Phillies' },
    { abbr: 'WSH', name: 'Washington Nationals' },
    // National League Central
    { abbr: 'CHC', name: 'Chicago Cubs' },
    { abbr: 'CIN', name: 'Cincinnati Reds' },
    { abbr: 'MIL', name: 'Milwaukee Brewers' },
    { abbr: 'PIT', name: 'Pittsburgh Pirates' },
    { abbr: 'STL', name: 'St. Louis Cardinals' },
    // National League West
    { abbr: 'ARI', name: 'Arizona Diamondbacks' },
    { abbr: 'COL', name: 'Colorado Rockies' },
    { abbr: 'LAD', name: 'Los Angeles Dodgers' },
    { abbr: 'SD', name: 'San Diego Padres' },
    { abbr: 'SF', name: 'San Francisco Giants' }
  ];

  function mlbLogoUrl(abbr) {
    // Use ESPN CDN logos for MLB where available. Most team codes map directly to lowercase abbr.
    // Fall back to a simple SVG placeholder if necessary.
    try {
      const code = (abbr || '').toLowerCase();
      return `https://a.espncdn.com/i/teamlogos/mlb/500/${code}.png`;
    } catch (e) {
      const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="72" height="72"><rect width="72" height="72" rx="12" fill="#002D62"/><text x="50%" y="50%" dominant-baseline="middle" text-anchor="middle" fill="#ffffff" font-family="Arial, sans-serif" font-size="18" font-weight="700">${abbr}</text></svg>`;
      return `data:image/svg+xml;utf8,${encodeURIComponent(svg)}`;
    }
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

  function getNflTeamUrl(abbr) {
    return `/NFL/Team?code=${encodeURIComponent(abbr)}`;
  }

  function buildNFLModalBodyHtml(columns, teamMap, logoUrl) {
    return columns
      .map(col => {
        const items = col.teams
          .map(abbr => {
            const t = teamMap.get(abbr);
            if (!t) return '';
            const url = getNflTeamUrl(abbr);
            return `
              <a class="nfl-team-modal__team" href="${url}" data-team="${abbr}">
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

  function getCbbTeamUrl(abbr) {
    return `/CollegeBasketball/Team?code=${encodeURIComponent(abbr)}`;
  }

  function buildCBBModalBodyHtml(columns, teamMap, logoUrl) {
    return columns
      .map(col => {
        const items = col.teams
          .map(abbr => {
            const t = teamMap.get(abbr);
            if (!t) return '';
            const url = getCbbTeamUrl(abbr);
            return `
              <a class="nfl-team-modal__team" href="${url}" data-team="${abbr}">
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

  // ?? College Football (CFB) ????????????????????????????????????????????
  // Team codes MUST match BiteTheBookie.Services.Implementations.CFBGamesService codes.
  const cfbTeams = [
    // ACC
    { abbr: 'BC', name: 'Boston College', logoId: '103' },
    { abbr: 'CAL', name: 'California', logoId: '25' },
    { abbr: 'CLEM', name: 'Clemson', logoId: '228' },
    { abbr: 'DUKE', name: 'Duke', logoId: '150' },
    { abbr: 'FSU', name: 'Florida State', logoId: '52' },
    { abbr: 'GT', name: 'Georgia Tech', logoId: '59' },
    { abbr: 'LOU', name: 'Louisville', logoId: '97' },
    { abbr: 'MIA', name: 'Miami', logoId: '2390' },
    { abbr: 'NCST', name: 'NC State', logoId: '152' },
    { abbr: 'UNC', name: 'North Carolina', logoId: '153' },
    { abbr: 'PITT', name: 'Pittsburgh', logoId: '221' },
    { abbr: 'SMU', name: 'SMU', logoId: '2567' },
    { abbr: 'STAN', name: 'Stanford', logoId: '24' },
    { abbr: 'SYR', name: 'Syracuse', logoId: '183' },
    { abbr: 'UVA', name: 'Virginia', logoId: '258' },
    { abbr: 'VT', name: 'Virginia Tech', logoId: '259' },
    { abbr: 'WAKE', name: 'Wake Forest', logoId: '154' },
    // Big Ten
    { abbr: 'ILL', name: 'Illinois', logoId: '356' },
    { abbr: 'IND', name: 'Indiana', logoId: '84' },
    { abbr: 'IOWA', name: 'Iowa', logoId: '2294' },
    { abbr: 'MD', name: 'Maryland', logoId: '120' },
    { abbr: 'MICH', name: 'Michigan', logoId: '130' },
    { abbr: 'MSU', name: 'Michigan State', logoId: '127' },
    { abbr: 'MINN', name: 'Minnesota', logoId: '135' },
    { abbr: 'NEB', name: 'Nebraska', logoId: '158' },
    { abbr: 'NW', name: 'Northwestern', logoId: '77' },
    { abbr: 'OSU', name: 'Ohio State', logoId: '194' },
    { abbr: 'ORE', name: 'Oregon', logoId: '2483' },
    { abbr: 'PSU', name: 'Penn State', logoId: '213' },
    { abbr: 'PUR', name: 'Purdue', logoId: '2509' },
    { abbr: 'RUT', name: 'Rutgers', logoId: '164' },
    { abbr: 'UCLA', name: 'UCLA', logoId: '26' },
    { abbr: 'USC', name: 'USC', logoId: '30' },
    { abbr: 'WASH', name: 'Washington', logoId: '264' },
    { abbr: 'WISC', name: 'Wisconsin', logoId: '275' },
    // Big 12
    { abbr: 'ARIZ', name: 'Arizona', logoId: '12' },
    { abbr: 'ASU', name: 'Arizona State', logoId: '9' },
    { abbr: 'BAY', name: 'Baylor', logoId: '239' },
    { abbr: 'BYU', name: 'BYU', logoId: '252' },
    { abbr: 'CIN', name: 'Cincinnati', logoId: '2132' },
    { abbr: 'COLO', name: 'Colorado', logoId: '38' },
    { abbr: 'HOU', name: 'Houston', logoId: '248' },
    { abbr: 'ISU', name: 'Iowa State', logoId: '66' },
    { abbr: 'KU', name: 'Kansas', logoId: '2305' },
    { abbr: 'KSU', name: 'Kansas State', logoId: '2306' },
    { abbr: 'OKST', name: 'Oklahoma State', logoId: '197' },
    { abbr: 'TCU', name: 'TCU', logoId: '2628' },
    { abbr: 'TTU', name: 'Texas Tech', logoId: '2641' },
    { abbr: 'UCF', name: 'UCF', logoId: '2116' },
    { abbr: 'UTAH', name: 'Utah', logoId: '254' },
    { abbr: 'WVU', name: 'West Virginia', logoId: '277' },
    // SEC
    { abbr: 'ALA', name: 'Alabama', logoId: '333' },
    { abbr: 'ARK', name: 'Arkansas', logoId: '8' },
    { abbr: 'AUB', name: 'Auburn', logoId: '2' },
    { abbr: 'FLA', name: 'Florida', logoId: '57' },
    { abbr: 'UGA', name: 'Georgia', logoId: '61' },
    { abbr: 'UK', name: 'Kentucky', logoId: '96' },
    { abbr: 'LSU', name: 'LSU', logoId: '99' },
    { abbr: 'MSST', name: 'Mississippi State', logoId: '344' },
    { abbr: 'MIZ', name: 'Missouri', logoId: '142' },
    { abbr: 'OU', name: 'Oklahoma', logoId: '201' },
    { abbr: 'MISS', name: 'Ole Miss', logoId: '145' },
    { abbr: 'SC', name: 'South Carolina', logoId: '2579' },
    { abbr: 'TENN', name: 'Tennessee', logoId: '2633' },
    { abbr: 'TEX', name: 'Texas', logoId: '251' },
    { abbr: 'TAMU', name: 'Texas A&M', logoId: '245' },
    { abbr: 'VAN', name: 'Vanderbilt', logoId: '238' },
    // Pac-12
    { abbr: 'ORST', name: 'Oregon State', logoId: '204' },
    { abbr: 'WSU', name: 'Washington State', logoId: '265' },
    // Independents
    { abbr: 'ND', name: 'Notre Dame', logoId: '87' },
    { abbr: 'CONN', name: 'UConn', logoId: '41' },
    { abbr: 'UMASS', name: 'UMass', logoId: '113' },
    // American Athletic
    { abbr: 'ARMY', name: 'Army', logoId: '349' },
    { abbr: 'CHAR', name: 'Charlotte', logoId: '2429' },
    { abbr: 'ECU', name: 'East Carolina', logoId: '151' },
    { abbr: 'FAU', name: 'Florida Atlantic', logoId: '2226' },
    { abbr: 'MEM', name: 'Memphis', logoId: '235' },
    { abbr: 'NAVY', name: 'Navy', logoId: '2426' },
    { abbr: 'UNT', name: 'North Texas', logoId: '249' },
    { abbr: 'RICE', name: 'Rice', logoId: '242' },
    { abbr: 'USF', name: 'South Florida', logoId: '58' },
    { abbr: 'TEM', name: 'Temple', logoId: '218' },
    { abbr: 'TUL', name: 'Tulane', logoId: '2655' },
    { abbr: 'TLSA', name: 'Tulsa', logoId: '202' },
    { abbr: 'UAB', name: 'UAB', logoId: '5' },
    { abbr: 'UTSA', name: 'UTSA', logoId: '2636' },
    // Conference USA
    { abbr: 'DEL', name: 'Delaware', logoId: '48' },
    { abbr: 'FIU', name: 'Florida International', logoId: '2229' },
    { abbr: 'JVST', name: 'Jacksonville State', logoId: '55' },
    { abbr: 'KENN', name: 'Kennesaw State', logoId: '338' },
    { abbr: 'LIB', name: 'Liberty', logoId: '2335' },
    { abbr: 'LT', name: 'Louisiana Tech', logoId: '2348' },
    { abbr: 'MTSU', name: 'Middle Tennessee', logoId: '2393' },
    { abbr: 'MOST', name: 'Missouri State', logoId: '2623' },
    { abbr: 'NMSU', name: 'New Mexico State', logoId: '166' },
    { abbr: 'SHSU', name: 'Sam Houston', logoId: '2534' },
    { abbr: 'UTEP', name: 'UTEP', logoId: '2638' },
    { abbr: 'WKU', name: 'Western Kentucky', logoId: '98' },
    // Mid-American
    { abbr: 'AKR', name: 'Akron', logoId: '2006' },
    { abbr: 'BALL', name: 'Ball State', logoId: '2050' },
    { abbr: 'BGSU', name: 'Bowling Green', logoId: '189' },
    { abbr: 'BUFF', name: 'Buffalo', logoId: '2084' },
    { abbr: 'CMU', name: 'Central Michigan', logoId: '2117' },
    { abbr: 'EMU', name: 'Eastern Michigan', logoId: '2199' },
    { abbr: 'KENT', name: 'Kent State', logoId: '2309' },
    { abbr: 'M-OH', name: 'Miami (OH)', logoId: '193' },
    { abbr: 'NIU', name: 'Northern Illinois', logoId: '2459' },
    { abbr: 'OHIO', name: 'Ohio', logoId: '195' },
    { abbr: 'TOL', name: 'Toledo', logoId: '2649' },
    { abbr: 'WMU', name: 'Western Michigan', logoId: '2711' },
    // Mountain West
    { abbr: 'AFA', name: 'Air Force', logoId: '2005' },
    { abbr: 'BSU', name: 'Boise State', logoId: '68' },
    { abbr: 'CSU', name: 'Colorado State', logoId: '36' },
    { abbr: 'FRES', name: 'Fresno State', logoId: '278' },
    { abbr: 'HAW', name: "Hawai'i", logoId: '62' },
    { abbr: 'NEV', name: 'Nevada', logoId: '2440' },
    { abbr: 'UNM', name: 'New Mexico', logoId: '167' },
    { abbr: 'SDSU', name: 'San Diego State', logoId: '21' },
    { abbr: 'SJSU', name: 'San Jose State', logoId: '23' },
    { abbr: 'UNLV', name: 'UNLV', logoId: '2439' },
    { abbr: 'USU', name: 'Utah State', logoId: '328' },
    { abbr: 'WYO', name: 'Wyoming', logoId: '2751' },
    // Sun Belt
    { abbr: 'APP', name: 'Appalachian State', logoId: '2026' },
    { abbr: 'ARST', name: 'Arkansas State', logoId: '2032' },
    { abbr: 'CCU', name: 'Coastal Carolina', logoId: '324' },
    { abbr: 'GASO', name: 'Georgia Southern', logoId: '290' },
    { abbr: 'GAST', name: 'Georgia State', logoId: '2247' },
    { abbr: 'JMU', name: 'James Madison', logoId: '256' },
    { abbr: 'UL', name: 'Louisiana', logoId: '309' },
    { abbr: 'ULM', name: 'Louisiana-Monroe', logoId: '2433' },
    { abbr: 'MRSH', name: 'Marshall', logoId: '276' },
    { abbr: 'ODU', name: 'Old Dominion', logoId: '295' },
    { abbr: 'USA', name: 'South Alabama', logoId: '6' },
    { abbr: 'USM', name: 'Southern Miss', logoId: '2572' },
    { abbr: 'TXST', name: 'Texas State', logoId: '326' },
    { abbr: 'TROY', name: 'Troy', logoId: '2653' }
  ];

  function cfbLogoUrl(abbr) {
    const t = cfbTeams.find(x => x.abbr === abbr);
    if (t && t.logoId) {
      return `https://a.espncdn.com/i/teamlogos/ncaa/500/${t.logoId}.png`;
    }
    const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="36" height="36"><rect width="36" height="36" rx="6" fill="#003366"/><text x="50%" y="50%" dominant-baseline="middle" text-anchor="middle" fill="#ffffff" font-family="Arial, sans-serif" font-size="10" font-weight="700">${abbr}</text></svg>`;
    return `data:image/svg+xml;utf8,${encodeURIComponent(svg)}`;
  }

  function getCfbTeamUrl(abbr) {
    return `/CollegeFootball/Team?code=${encodeURIComponent(abbr)}`;
  }

  function buildCFBModalBodyHtml(columns, teamMap, logoUrl) {
    return columns
      .map(col => {
        const items = col.teams
          .map(abbr => {
            const t = teamMap.get(abbr);
            if (!t) return '';
            const url = getCfbTeamUrl(abbr);
            return `
              <a class="nfl-team-modal__team" href="${url}" data-team="${abbr}">
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

  const cfbColumns = [
    { title: 'ACC', teams: ['BC', 'CAL', 'CLEM', 'DUKE', 'FSU', 'GT', 'LOU', 'MIA', 'NCST', 'UNC', 'PITT', 'SMU', 'STAN', 'SYR', 'UVA', 'VT', 'WAKE'] },
    { title: 'Big Ten', teams: ['ILL', 'IND', 'IOWA', 'MD', 'MICH', 'MSU', 'MINN', 'NEB', 'NW', 'OSU', 'ORE', 'PSU', 'PUR', 'RUT', 'UCLA', 'USC', 'WASH', 'WISC'] },
    { title: 'Big 12', teams: ['ARIZ', 'ASU', 'BAY', 'BYU', 'CIN', 'COLO', 'HOU', 'ISU', 'KU', 'KSU', 'OKST', 'TCU', 'TTU', 'UCF', 'UTAH', 'WVU'] },
    { title: 'SEC', teams: ['ALA', 'ARK', 'AUB', 'FLA', 'UGA', 'UK', 'LSU', 'MSST', 'MIZ', 'OU', 'MISS', 'SC', 'TENN', 'TEX', 'TAMU', 'VAN'] },
    { title: 'Pac-12', teams: ['ORST', 'WSU'] },
    { title: 'Independents', teams: ['ND', 'CONN', 'UMASS'] },
    { title: 'American', teams: ['ARMY', 'CHAR', 'ECU', 'FAU', 'MEM', 'NAVY', 'UNT', 'RICE', 'USF', 'TEM', 'TUL', 'TLSA', 'UAB', 'UTSA'] },
    { title: 'Conference USA', teams: ['DEL', 'FIU', 'JVST', 'KENN', 'LIB', 'LT', 'MTSU', 'MOST', 'NMSU', 'SHSU', 'UTEP', 'WKU'] },
    { title: 'Mid-American', teams: ['AKR', 'BALL', 'BGSU', 'BUFF', 'CMU', 'EMU', 'KENT', 'M-OH', 'NIU', 'OHIO', 'TOL', 'WMU'] },
    { title: 'Mountain West', teams: ['AFA', 'BSU', 'CSU', 'FRES', 'HAW', 'NEV', 'UNM', 'SDSU', 'SJSU', 'UNLV', 'USU', 'WYO'] },
    { title: 'Sun Belt', teams: ['APP', 'ARST', 'CCU', 'GASO', 'GAST', 'JMU', 'UL', 'ULM', 'MRSH', 'ODU', 'USA', 'USM', 'TXST', 'TROY'] }
  ];

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

  const nhlTeams = [
    // Atlantic Division
    { abbr: 'BOS', name: 'Boston Bruins' },
    { abbr: 'BUF', name: 'Buffalo Sabres' },
    { abbr: 'DET', name: 'Detroit Red Wings' },
    { abbr: 'FLA', name: 'Florida Panthers' },
    { abbr: 'MTL', name: 'Montreal Canadiens' },
    { abbr: 'OTT', name: 'Ottawa Senators' },
    { abbr: 'TBL', name: 'Tampa Bay Lightning' },
    { abbr: 'TOR', name: 'Toronto Maple Leafs' },
    // Metropolitan Division
    { abbr: 'CAR', name: 'Carolina Hurricanes' },
    { abbr: 'CBJ', name: 'Columbus Blue Jackets' },
    { abbr: 'NJD', name: 'New Jersey Devils' },
    { abbr: 'NYI', name: 'New York Islanders' },
    { abbr: 'NYR', name: 'New York Rangers' },
    { abbr: 'PHI', name: 'Philadelphia Flyers' },
    { abbr: 'PIT', name: 'Pittsburgh Penguins' },
    { abbr: 'WSH', name: 'Washington Capitals' },
    // Central Division
    { abbr: 'ARI', name: 'Arizona Coyotes' },
    { abbr: 'CHI', name: 'Chicago Blackhawks' },
    { abbr: 'COL', name: 'Colorado Avalanche' },
    { abbr: 'DAL', name: 'Dallas Stars' },
    { abbr: 'MIN', name: 'Minnesota Wild' },
    { abbr: 'NSH', name: 'Nashville Predators' },
    { abbr: 'STL', name: 'St. Louis Blues' },
    { abbr: 'WPG', name: 'Winnipeg Jets' },
    // Pacific Division
    { abbr: 'ANA', name: 'Anaheim Ducks' },
    { abbr: 'CGY', name: 'Calgary Flames' },
    { abbr: 'EDM', name: 'Edmonton Oilers' },
    { abbr: 'LAK', name: 'Los Angeles Kings' },
    { abbr: 'SEA', name: 'Seattle Kraken' },
    { abbr: 'SJS', name: 'San Jose Sharks' },
    { abbr: 'VAN', name: 'Vancouver Canucks' },
    { abbr: 'VGK', name: 'Vegas Golden Knights' }
  ];

  function nhlLogoUrl(abbr) {
    // Real team logos from ESPN's public CDN. A few teams use ESPN-specific codes
    // that differ from our internal abbreviations.
    const espnCode = {
      LAK: 'la',   // Los Angeles Kings
      NJD: 'nj',   // New Jersey Devils
      SJS: 'sj',   // San Jose Sharks
      TBL: 'tb'    // Tampa Bay Lightning
    }[abbr] || abbr.toLowerCase();
    return `https://a.espncdn.com/i/teamlogos/nhl/500/${espnCode}.png`;
  }

  const nhlColumns = [
    { title: 'Atlantic', teams: ['BOS', 'BUF', 'DET', 'FLA', 'MTL', 'OTT', 'TBL', 'TOR'] },
    { title: 'Metropolitan', teams: ['CAR', 'CBJ', 'NJD', 'NYI', 'NYR', 'PHI', 'PIT', 'WSH'] },
    { title: 'Central', teams: ['ARI', 'CHI', 'COL', 'DAL', 'MIN', 'NSH', 'STL', 'WPG'] },
    { title: 'Pacific', teams: ['ANA', 'CGY', 'EDM', 'LAK', 'SEA', 'SJS', 'VAN', 'VGK'] }
  ];

  const cbbColumns = [
    { title: 'ACC', teams: ['DUKE', 'UNC', 'UVA', 'CLEM', 'NCSU', 'WAKE', 'VT', 'MIA', 'FSU', 'LOU', 'PITT', 'SYR', 'BC', 'GT', 'ND'] },
    { title: 'Big Ten', teams: ['ILL', 'IND', 'IOWA', 'MD', 'MICH', 'MSU', 'MINN', 'NEB', 'NW', 'OSU', 'PSU', 'PUR', 'RUT', 'WIS'] },
    { title: 'Big 12', teams: ['BAY', 'ISU', 'KU', 'KSU', 'OU', 'OST', 'TCU', 'TEX', 'TTU', 'WVU'] },
    { title: 'SEC', teams: ['ALA', 'ARK', 'AUB', 'FLA', 'UGA', 'UK', 'LSU', 'MISS', 'MST', 'USC', 'TENN', 'TAMU', 'VAN'] },
    { title: 'Pac-12', teams: ['ARIZ', 'ASU', 'CAL', 'COLO', 'ORE', 'ORST', 'STAN', 'UCLA', 'WASH', 'WSU'] },
    { title: 'Big East', teams: ['BUT', 'CRE', 'DPU', 'GTWN', 'MARQ', 'PROV', 'SHU', 'SJU', 'VILL', 'XAV'] }
  ];

  const mlbColumns = [
    { title: 'AL East', teams: ['BAL', 'BOS', 'NYY', 'TB', 'TOR'] },
    { title: 'AL Central', teams: ['CWS', 'CLE', 'DET', 'KC', 'MIN'] },
    { title: 'AL West', teams: ['HOU', 'LAA', 'OAK', 'SEA', 'TEX'] },
    { title: 'NL East', teams: ['ATL', 'MIA', 'NYM', 'PHI', 'WSH'] },
    { title: 'NL Central', teams: ['CHC', 'CIN', 'MIL', 'PIT', 'STL'] },
    { title: 'NL West', teams: ['ARI', 'COL', 'LAD', 'SD', 'SF'] }
  ];

  function showTicker(league) {
    const tickers = ['nfl', 'nba', 'nhl', 'ncaa', 'ncaaf', 'mlb'];
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

    function show() {
      modal.show();
      showTicker(options.league);
    }

    // Hover: Only change ticker (don't show modal)
    link.addEventListener('mouseenter', () => {
      showTicker(options.league);
    });

    // Click: Show modal AND change ticker
    link.addEventListener('click', (e) => {
      e.preventDefault();
      show();
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
  }

  document.addEventListener('DOMContentLoaded', function () {
    initLeagueTeamModal({
      league: 'nfl',
      modalId: 'nflTeamModal',
      teams: nflTeams,
      columns: nflColumns,
      logoUrl: nflLogoUrl,
      buildFunction: buildNFLModalBodyHtml,
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

    initLeagueTeamModal({
      league: 'ncaaf',
      modalId: 'cfbTeamModal',
      teams: cfbTeams,
      columns: cfbColumns,
      logoUrl: cfbLogoUrl,
      buildFunction: buildCFBModalBodyHtml,
      oddsLink: { url: '/Odds/CFB', label: 'View CFB Odds' }
    });


    initLeagueTeamModal({
      league: 'mlb',
      modalId: 'mlbTeamModal',
      teams: mlbTeams,
      columns: mlbColumns,
      logoUrl: mlbLogoUrl,
      oddsLink: { url: '/Odds/MLB', label: 'View MLB Odds' }
    });

    initLeagueTeamModal({
      league: 'nhl',
      modalId: 'nhlTeamModal',
      teams: nhlTeams,
      columns: nhlColumns,
      logoUrl: nhlLogoUrl,
      oddsLink: { url: '/Odds/NHL', label: 'View NHL Odds' }
    });
  });

  navigator.permissions.query({ name: "geolocation" })
  // or, if storing:
  const query = navigator.permissions.query.bind(navigator.permissions);
  query({ name: "geolocation" });
})();
 






