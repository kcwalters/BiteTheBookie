(() => {
    "use strict";

    const tickerIdsByLeague = {
        nfl: "nfl-ticker",
        nba: "nba-ticker",
        nhl: "nhl-ticker",
        ncaa: "ncaa-ticker",
    };

    function showTicker(league) {
        const leagueKey = (league ?? "").toString().toLowerCase();
        const targetId = tickerIdsByLeague[leagueKey];
        if (!targetId) {
            return;
        }

        for (const id of Object.values(tickerIdsByLeague)) {
            const el = document.getElementById(id);
            if (!el) {
                continue;
            }

            el.classList.toggle("d-none", id !== targetId);
        }
    }

    function wireLeagueHoverSwitching() {
        // Option 1: a specific NFL link id
        const nflLink = document.getElementById("nfl-link");
        if (nflLink) {
            nflLink.addEventListener("mouseenter", () => showTicker("nfl"));
        }

        // Option 2: any link with data-league="nfl"
        document.querySelectorAll('[data-league="nfl"]').forEach((el) => {
            el.addEventListener("mouseenter", () => showTicker("nfl"));
        });
    }

    document.addEventListener("DOMContentLoaded", () => {
        // Pick a default ticker to show (optional)
        showTicker("nba");

        wireLeagueHoverSwitching();
    });
})();