// Client-side filtering for league landing-page scoreboards.
// Filters already-rendered game cards by status, day/week group, and team.
// No server round-trip, so it adds zero extra ESPN API calls.
(function () {
    "use strict";

    function initContainer(root) {
        var filters = root.querySelector("[data-game-filters]");
        var gamesRoot = root.querySelector("[data-games-root]");
        if (!filters || !gamesRoot) {
            return;
        }

        var groups = Array.prototype.slice.call(gamesRoot.querySelectorAll("[data-group]"));
        var cards = Array.prototype.slice.call(gamesRoot.querySelectorAll("[data-game-card]"));

        var groupSelect = filters.querySelector("[data-filter-group]");
        var teamSelect = filters.querySelector("[data-filter-team]");
        var statusButtons = Array.prototype.slice.call(filters.querySelectorAll("[data-filter-status]"));
        var resetBtn = filters.querySelector("[data-filter-reset]");
        var countEl = filters.querySelector("[data-filter-count]");

        // Populate the day/week dropdown in document order.
        groups.forEach(function (g) {
            var label = g.getAttribute("data-group");
            if (!label) {
                return;
            }
            var opt = document.createElement("option");
            opt.value = label;
            opt.textContent = label;
            groupSelect.appendChild(opt);
        });

        // Populate the team dropdown with the sorted unique set of teams on the page.
        var teamSet = {};
        cards.forEach(function (c) {
            (c.getAttribute("data-teams") || "").split("|").forEach(function (t) {
                t = t.trim();
                if (t) {
                    teamSet[t] = true;
                }
            });
        });
        Object.keys(teamSet).sort().forEach(function (t) {
            var opt = document.createElement("option");
            opt.value = t.toLowerCase();
            opt.textContent = t;
            teamSelect.appendChild(opt);
        });

        var state = { status: "all", group: "all", team: "all" };

        function apply() {
            var visible = 0;

            cards.forEach(function (c) {
                var cardStatus = c.getAttribute("data-status") || "";
                var cardGroup = c.getAttribute("data-card-group") || "";
                var cardTeams = (c.getAttribute("data-teams") || "").toLowerCase().split("|");

                var ok =
                    (state.status === "all" || cardStatus === state.status) &&
                    (state.group === "all" || cardGroup === state.group) &&
                    (state.team === "all" || cardTeams.indexOf(state.team) !== -1);

                c.style.display = ok ? "" : "none";
                if (ok) {
                    visible++;
                }
            });

            // Hide group headers that have no visible cards.
            groups.forEach(function (g) {
                var any = Array.prototype.slice.call(g.querySelectorAll("[data-game-card]"));
                var shown = any.some(function (c) { return c.style.display !== "none"; });
                g.style.display = shown ? "" : "none";
            });

            if (countEl) {
                countEl.textContent = visible + (visible === 1 ? " game" : " games");
            }
        }

        statusButtons.forEach(function (b) {
            b.addEventListener("click", function () {
                state.status = b.getAttribute("data-filter-status");
                statusButtons.forEach(function (x) { x.classList.remove("active"); });
                b.classList.add("active");
                apply();
            });
        });

        if (groupSelect) {
            groupSelect.addEventListener("change", function () {
                state.group = groupSelect.value;
                apply();
            });
        }

        if (teamSelect) {
            teamSelect.addEventListener("change", function () {
                state.team = teamSelect.value;
                apply();
            });
        }

        if (resetBtn) {
            resetBtn.addEventListener("click", function () {
                state = { status: "all", group: "all", team: "all" };
                if (groupSelect) { groupSelect.value = "all"; }
                if (teamSelect) { teamSelect.value = "all"; }
                statusButtons.forEach(function (x) {
                    x.classList.toggle("active", x.getAttribute("data-filter-status") === "all");
                });
                apply();
            });
        }

        apply();
    }

    document.addEventListener("DOMContentLoaded", function () {
        Array.prototype.slice
            .call(document.querySelectorAll("[data-filterable-games]"))
            .forEach(initContainer);
    });
})();
