(function () {
  const teams = [
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

  // Public, stable CDN for team logos (no copyrighted ESPN assets in repo)
  // Source: https://github.com/StevenDaily/nfl-football-logos (raw GitHub CDN)
  function logoUrl(abbr) {
    return `https://raw.githubusercontent.com/StevenDaily/nfl-football-logos/master/svg/${abbr}.svg`;
  }

  function buildModalBodyHtml() {
    const columns = [
      { title: 'AFC East', teams: ['BUF', 'MIA', 'NE', 'NYJ'] },
      { title: 'AFC North', teams: ['BAL', 'CIN', 'CLE', 'PIT'] },
      { title: 'AFC South', teams: ['HOU', 'IND', 'JAX', 'TEN'] },
      { title: 'AFC West', teams: ['DEN', 'KC', 'LAC', 'LV'] },
      { title: 'NFC East', teams: ['DAL', 'NYG', 'PHI', 'WAS'] },
      { title: 'NFC North', teams: ['CHI', 'DET', 'GB', 'MIN'] },
      { title: 'NFC South', teams: ['ATL', 'CAR', 'NO', 'TB'] },
      { title: 'NFC West', teams: ['ARI', 'LAR', 'SEA', 'SF'] }
    ];

    const teamMap = new Map(teams.map(t => [t.abbr, t]));

    return columns
      .map(col => {
        const items = col.teams
          .map(abbr => {
            const t = teamMap.get(abbr);
            if (!t) return '';
            return `
              <a class="nfl-team-modal__team" href="https://localhost:32771/img/cin.png" data-team="${abbr}">
                <img class="nfl-team-modal__logo" src="img/NFL/cin.png" alt="${t.name}" loading="lazy" />
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

  function initNflTeamModal() {
    const nflLink = document.querySelector('.league-menu a[data-league="nfl"]');
    const modalEl = document.getElementById('nflTeamModal');
    const modalBody = modalEl?.querySelector('.modal-body');

    if (!nflLink || !modalEl || !modalBody || !window.bootstrap?.Modal) return;

    modalBody.innerHTML = `<div class="nfl-team-modal__grid">${buildModalBodyHtml()}</div>`;

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
      }, 150);
    }

    function show() {
      clearHideTimer();
      modal.show();
      
      // Trigger ticker visibility for NFL (on both hover and click)
      const nflTicker = document.getElementById('nfl-ticker');
      if (nflTicker) {
        // Show NFL ticker, hide others
        const tickers = ['nfl', 'nba', 'nhl', 'ncaa'];
        tickers.forEach(league => {
          const ticker = document.getElementById(league + '-ticker');
          if (ticker) {
            ticker.style.display = (league === 'nfl') ? '' : 'none';
          }
        });
        
        // Save preference
        try {
          localStorage.setItem('selectedSportsTicker', 'nfl');
        } catch {}
        
        // Dispatch event for other listeners
        document.dispatchEvent(new CustomEvent('sportsTickerSelected', { detail: { league: 'nfl' } }));
      }
    }

    // Hover behaviors
    nflLink.addEventListener('mouseenter', show);
    nflLink.addEventListener('focus', show);
    nflLink.addEventListener('mouseleave', scheduleHide);

    // Click on NFL link: show modal AND show NFL ticker
    nflLink.addEventListener('click', (e) => {
      e.preventDefault();
      show();
    });

    modalEl.addEventListener('mouseenter', clearHideTimer);
    modalEl.addEventListener('mouseleave', scheduleHide);

    // Click team: placeholder hook
    modalEl.addEventListener('click', (e) => {
      const team = e.target.closest('.nfl-team-modal__team');
      if (!team) return;
      e.preventDefault();
      // Future: navigate to team page
      modal.hide();
    });

    // Ensure timer cleared on hide
    modalEl.addEventListener('hidden.bs.modal', clearHideTimer);
  }

  document.addEventListener('DOMContentLoaded', initNflTeamModal);

  navigator.permissions.query({ name: "geolocation" })
  // or, if storing:
  const query = navigator.permissions.query.bind(navigator.permissions);
  query({ name: "geolocation" });
})();
