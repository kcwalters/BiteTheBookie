// Progressive-enhancement hover dropdowns for the league nav.
// For each ".league-link" in the top nav we derive its controller from the
// rendered href (e.g. /NFL, /CollegeFootball) and lazily fetch that league's
// teams from the controller's NavTeams JSON endpoint, then render a hover menu.
// No changes to the server-rendered nav markup are required.
(function () {
    "use strict";

    // Maps the first path segment of a league link to its controller name.
    // The controller name is used to build both the NavTeams feed URL and the
    // per-team "Team" URL, so links resolve correctly for every league.
    var CONTROLLER_ALIASES = {
        nfl: "NFL",
        mlb: "MLB",
        nba: "NBA",
        nhl: "NHL",
        collegefootball: "CollegeFootball",
        cfb: "CollegeFootball",
        collegebasketball: "CollegeBasketball",
        cbb: "CollegeBasketball"
    };

    function resolveController(link) {
        // Prefer an explicit data-league hint if present, else the href path.
        var hint = (link.getAttribute("data-league") || "").toLowerCase();
        if (CONTROLLER_ALIASES[hint]) {
            return CONTROLLER_ALIASES[hint];
        }

        try {
            var url = new URL(link.href, window.location.origin);
            var seg = url.pathname.split("/").filter(Boolean)[0] || "";
            var key = seg.toLowerCase();
            if (CONTROLLER_ALIASES[key]) {
                return CONTROLLER_ALIASES[key];
            }
            // Fall back to the raw first segment (already the controller name).
            return seg || null;
        } catch (e) {
            return null;
        }
    }

    function buildMenu(controller, teams) {
        var menu = document.createElement("div");
        menu.className = "league-dropdown-menu";

        if (!teams || !teams.length) {
            var empty = document.createElement("div");
            empty.className = "league-dropdown-empty";
            empty.textContent = "No teams available.";
            menu.appendChild(empty);
            return menu;
        }

        // Group teams by division/conference, preserving first-seen order.
        var groups = [];
        var groupsByName = {};
        teams.forEach(function (team) {
            var division = (team.division || "Other");
            var group = groupsByName[division];
            if (!group) {
                group = { name: division, teams: [] };
                groupsByName[division] = group;
                groups.push(group);
            }
            group.teams.push(team);
        });

        var columns = document.createElement("div");
        columns.className = "league-dropdown-columns";

        groups.forEach(function (group) {
            var column = document.createElement("div");
            column.className = "league-dropdown-column";

            var header = document.createElement("div");
            header.className = "league-dropdown-header";
            header.textContent = group.name;
            column.appendChild(header);

            group.teams.forEach(function (team) {
                var a = document.createElement("a");
                a.className = "league-dropdown-item";
                a.href = "/" + controller + "/Team?code=" + encodeURIComponent(team.code);

                if (team.logo) {
                    var img = document.createElement("img");
                    img.src = team.logo;
                    img.alt = "";
                    img.className = "league-dropdown-logo";
                    img.loading = "lazy";
                    a.appendChild(img);
                }

                var span = document.createElement("span");
                span.textContent = team.name;
                a.appendChild(span);

                column.appendChild(a);
            });

            columns.appendChild(column);
        });

        menu.appendChild(columns);
        return menu;
    }

    function attach(link) {
        var controller = resolveController(link);
        if (!controller) {
            return;
        }

        var li = link.closest("li") || link.parentElement;
        if (!li) {
            return;
        }
        li.classList.add("league-dropdown");

        var loaded = false;
        var loading = false;

        function load() {
            if (loaded || loading) {
                return;
            }
            loading = true;

            fetch("/" + controller + "/NavTeams", {
                headers: { "Accept": "application/json" }
            })
                .then(function (r) { return r.ok ? r.json() : []; })
                .then(function (teams) {
                    li.appendChild(buildMenu(controller, teams));
                    loaded = true;
                })
                .catch(function () { /* leave the plain link on failure */ })
                .finally(function () { loading = false; });
        }

        // Load on first hover/focus so we don't fire six requests on page load.
        li.addEventListener("mouseenter", load);
        link.addEventListener("focus", load);
    }

    function init() {
        var links = document.querySelectorAll(".league-nav .league-link");
        Array.prototype.forEach.call(links, attach);
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", init);
    } else {
        init();
    }
})();
