#!/usr/bin/env python3
"""
Patches GameSimulationService.cs to:
1. Add ESPN roster fetch + roster block injection into NFL, CFB, NHL prompts
2. Add BuildRosterSection, GetNflEspnCode, GetNhlEspnCode, GetCfbEspnCode helpers
"""
import re, sys

SVC = r"C:\Users\kcwal\source\repos\BiteTheBookie\BiteTheBookie\Services\Implementations\GameSimulationService.cs"

with open(SVC, encoding="utf-8") as fh:
	src = fh.read()

# ── Helper: build the roster-fetch block to insert at start of each sport try-block ──
def roster_fetch_block(sport, code_fn, roster_fn, away_var, home_var):
	return f"""
				// ── Fetch live rosters from ESPN ─────────────────────────────
				var {away_var}Code = {code_fn}(awayTeam);
				var {home_var}Code = {code_fn}(homeTeam);
				var {sport}Rosters = await Task.WhenAll(
					string.IsNullOrEmpty({away_var}Code) ? Task.FromResult(new List<string>()) : _espnClient.{roster_fn}({away_var}Code, cancellationToken),
					string.IsNullOrEmpty({home_var}Code) ? Task.FromResult(new List<string>()) : _espnClient.{roster_fn}({home_var}Code, cancellationToken));
				var {away_var}Roster = {sport}Rosters[0];
				var {home_var}Roster = {sport}Rosters[1];
				_logger.LogInformation("{sport.upper()} ESPN rosters: {{Away}}={{AwayCount}}, {{Home}}={{HomeCount}}",
					awayTeam, {away_var}Roster.Count, homeTeam, {home_var}Roster.Count);
				var {sport}RosterSection = BuildRosterSection(awayTeam, {away_var}Roster, homeTeam, {home_var}Roster, today);
"""

# ──────────────────────────── NFL ────────────────────────────────────────────
NFL_TODAY = '                var today  = DateTime.UtcNow.ToString("yyyy-MM-dd");\r\n                var season = "2025 NFL season";\r\n'
NFL_TODAY_NEW = (
	'                var today  = DateTime.UtcNow.ToString("yyyy-MM-dd");\r\n'
	'                var season = "2025 NFL season";\r\n'
	+ roster_fetch_block("nfl", "GetNflEspnCode", "GetNflRosterAsync", "awayNfl", "homeNfl").replace("\n", "\r\n")
)

NFL_ROSTER_GUIDANCE = (
	"ROSTER GUIDANCE:\r\n"
	"- Reference realistic, plausible CURRENT NFL players for each team (QB, RB, WR, TE, key defenders).\r\n"
	"- Use only players who plausibly play for {awayTeam} or {homeTeam} in the {season}.\r\n"
	"- A player who was traded, released, or signed elsewhere must NOT appear on his former team.\r\n"
	"- Do NOT invent absurd names or reference players from other teams.\r\n"
	"\r\n"
	"SIMULATION REQUIREMENTS:\r\n"
)
NFL_ROSTER_GUIDANCE_NEW = (
	"{nflRosterSection}\r\n"
	"SIMULATION REQUIREMENTS:\r\n"
)

NFL_FINAL_CHECK = (
	"FINAL CHECK BEFORE RESPONDING:\r\n"
	"Confirm every stat is a FOOTBALL stat and the final score is a realistic NFL score."
)
NFL_FINAL_CHECK_NEW = (
	"FINAL CHECK: Review every player name. Remove any not in the AUTHORITATIVE ROSTERS above."
)

# ──────────────────────────── CFB ────────────────────────────────────────────
CFB_TODAY = '                var today  = DateTime.UtcNow.ToString("yyyy-MM-dd");\r\n                var season = "2025 college football season";\r\n'
CFB_TODAY_NEW = (
	'                var today  = DateTime.UtcNow.ToString("yyyy-MM-dd");\r\n'
	'                var season = "2025 college football season";\r\n'
	+ roster_fetch_block("cfb", "GetCfbEspnCode", "GetCfbRosterAsync", "awayCfb", "homeCfb").replace("\n", "\r\n")
)

CFB_ROSTER_GUIDANCE = (
	"ROSTER GUIDANCE:\r\n"
	"- Reference realistic, plausible current players for each program (QB, RB, WR, key defenders).\r\n"
	"- Use only players who plausibly play for {awayTeam} or {homeTeam} in the {season}.\r\n"
	"- Do NOT invent absurd names or reference players from other programs.\r\n"
	"\r\n"
	"SIMULATION REQUIREMENTS:\r\n"
)
CFB_ROSTER_GUIDANCE_NEW = (
	"{cfbRosterSection}\r\n"
	"SIMULATION REQUIREMENTS:\r\n"
)

CFB_FINAL_CHECK = (
	"FINAL CHECK BEFORE RESPONDING:\r\n"
	"Confirm every stat is a FOOTBALL stat and the final score is a realistic football score."
)
CFB_FINAL_CHECK_NEW = (
	"FINAL CHECK: Review every player name. Remove any not in the AUTHORITATIVE ROSTERS above."
)

# ──────────────────────────── NHL ────────────────────────────────────────────
NHL_TODAY = '                var today  = DateTime.UtcNow.ToString("yyyy-MM-dd");\r\n                var season = "2025-26 NHL season";\r\n'
NHL_TODAY_NEW = (
	'                var today  = DateTime.UtcNow.ToString("yyyy-MM-dd");\r\n'
	'                var season = "2025-26 NHL season";\r\n'
	+ roster_fetch_block("nhl", "GetNhlEspnCode", "GetNhlRosterAsync", "awayNhl", "homeNhl").replace("\n", "\r\n")
)

NHL_ROSTER_GUIDANCE = (
	"ROSTER GUIDANCE:\r\n"
	"- Reference realistic, plausible CURRENT NHL players for each team (forwards, defensemen, starting goaltender).\r\n"
	"- Use only players who plausibly play for {awayTeam} or {homeTeam} in the {season}.\r\n"
	"- A player who was traded, released, or signed elsewhere must NOT appear on his former team.\r\n"
	"- Do NOT invent absurd names or reference players from other teams.\r\n"
	"\r\n"
	"SIMULATION REQUIREMENTS:\r\n"
)
NHL_ROSTER_GUIDANCE_NEW = (
	"{nhlRosterSection}\r\n"
	"SIMULATION REQUIREMENTS:\r\n"
)

NHL_FINAL_CHECK = (
	"FINAL CHECK BEFORE RESPONDING:\r\n"
	"Confirm every stat is a HOCKEY stat and the final score is a realistic NHL score."
)
NHL_FINAL_CHECK_NEW = (
	"FINAL CHECK: Review every player name. Remove any not in the AUTHORITATIVE ROSTERS above."
)

# ── Apply all replacements ────────────────────────────────────────────────────
replacements = [
	(NFL_TODAY,          NFL_TODAY_NEW,          "NFL today+season block"),
	(NFL_ROSTER_GUIDANCE, NFL_ROSTER_GUIDANCE_NEW, "NFL roster guidance"),
	(NFL_FINAL_CHECK,    NFL_FINAL_CHECK_NEW,    "NFL final check"),
	(CFB_TODAY,          CFB_TODAY_NEW,          "CFB today+season block"),
	(CFB_ROSTER_GUIDANCE, CFB_ROSTER_GUIDANCE_NEW, "CFB roster guidance"),
	(CFB_FINAL_CHECK,    CFB_FINAL_CHECK_NEW,    "CFB final check"),
	(NHL_TODAY,          NHL_TODAY_NEW,          "NHL today+season block"),
	(NHL_ROSTER_GUIDANCE, NHL_ROSTER_GUIDANCE_NEW, "NHL roster guidance"),
	(NHL_FINAL_CHECK,    NHL_FINAL_CHECK_NEW,    "NHL final check"),
]

for old, new, label in replacements:
	if old in src:
		src = src.replace(old, new, 1)
		print(f"OK: {label}")
	else:
		print(f"MISS: {label}")

# ── Add helper methods before the closing brace of the class ─────────────────
HELPERS = r"""
		// ── ESPN team code helpers ────────────────────────────────────────────

		private static string BuildRosterSection(
			string awayTeam, List<string> awayRoster,
			string homeTeam, List<string> homeRoster,
			string today)
		{
			if (awayRoster.Count == 0 && homeRoster.Count == 0)
				return "\nROSTER GUIDANCE:\n- Reference realistic, plausible current players.\n- Do NOT invent players or reference anyone not on the team.\n\n";

			var awayBlock = awayRoster.Count > 0
				? string.Join("\n", awayRoster.Select(n => $"  \u2022 {n}"))
				: "  (roster unavailable — use general knowledge)";
			var homeBlock = homeRoster.Count > 0
				? string.Join("\n", homeRoster.Select(n => $"  \u2022 {n}"))
				: "  (roster unavailable — use general knowledge)";

			return $@"
AUTHORITATIVE ROSTERS — {today}
Use ONLY players listed below. Do NOT reference any player not on this list.

{awayTeam} ACTIVE ROSTER:
{awayBlock}

{homeTeam} ACTIVE ROSTER:
{homeBlock}

";
		}

		private static string GetNflEspnCode(string teamName) => teamName switch
		{
			"Arizona Cardinals"     => "ari", "Atlanta Falcons"        => "atl",
			"Baltimore Ravens"      => "bal", "Buffalo Bills"          => "buf",
			"Carolina Panthers"     => "car", "Chicago Bears"          => "chi",
			"Cincinnati Bengals"    => "cin", "Cleveland Browns"       => "cle",
			"Dallas Cowboys"        => "dal", "Denver Broncos"         => "den",
			"Detroit Lions"         => "det", "Green Bay Packers"      => "gb",
			"Houston Texans"        => "hou", "Indianapolis Colts"     => "ind",
			"Jacksonville Jaguars"  => "jax", "Kansas City Chiefs"     => "kc",
			"Las Vegas Raiders"     => "lv",  "Los Angeles Chargers"   => "lac",
			"Los Angeles Rams"      => "lar", "Miami Dolphins"         => "mia",
			"Minnesota Vikings"     => "min", "New England Patriots"   => "ne",
			"New Orleans Saints"    => "no",  "New York Giants"        => "nyg",
			"New York Jets"         => "nyj", "Philadelphia Eagles"    => "phi",
			"Pittsburgh Steelers"   => "pit", "San Francisco 49ers"    => "sf",
			"Seattle Seahawks"      => "sea", "Tampa Bay Buccaneers"   => "tb",
			"Tennessee Titans"      => "ten", "Washington Commanders"  => "wsh",
			_ => string.Empty
		};

		private static string GetNhlEspnCode(string teamName) => teamName switch
		{
			"Anaheim Ducks"          => "ana", "Arizona Coyotes"        => "ari",
			"Boston Bruins"          => "bos", "Buffalo Sabres"         => "buf",
			"Calgary Flames"         => "cgy", "Carolina Hurricanes"    => "car",
			"Chicago Blackhawks"     => "chi", "Colorado Avalanche"     => "col",
			"Columbus Blue Jackets"  => "cbj", "Dallas Stars"           => "dal",
			"Detroit Red Wings"      => "det", "Edmonton Oilers"        => "edm",
			"Florida Panthers"       => "fla", "Los Angeles Kings"      => "la",
			"Minnesota Wild"         => "min", "Montreal Canadiens"     => "mtl",
			"Nashville Predators"    => "nsh", "New Jersey Devils"      => "nj",
			"New York Islanders"     => "nyi", "New York Rangers"       => "nyr",
			"Ottawa Senators"        => "ott", "Philadelphia Flyers"    => "phi",
			"Pittsburgh Penguins"    => "pit", "San Jose Sharks"        => "sj",
			"Seattle Kraken"         => "sea", "St. Louis Blues"        => "stl",
			"Tampa Bay Lightning"    => "tb",  "Toronto Maple Leafs"    => "tor",
			"Utah Hockey Club"       => "uta", "Vancouver Canucks"      => "van",
			"Vegas Golden Knights"   => "vgk", "Washington Capitals"   => "wsh",
			"Winnipeg Jets"          => "wpg",
			_ => string.Empty
		};

		private static string GetCfbEspnCode(string teamName) => teamName switch
		{
			"Alabama"           => "alabama",       "Arizona"            => "arizona",
			"Arizona State"     => "arizona-state", "Arkansas"           => "arkansas",
			"Auburn"            => "auburn",        "Baylor"             => "baylor",
			"Boise State"       => "boise-state",   "BYU"                => "byu",
			"California"        => "california",    "Clemson"            => "clemson",
			"Colorado"          => "colorado",      "Duke"               => "duke",
			"Florida"           => "florida",       "Florida State"      => "florida-state",
			"Georgia"           => "georgia",       "Georgia Tech"       => "georgia-tech",
			"Houston"           => "houston",       "Illinois"           => "illinois",
			"Indiana"           => "indiana",       "Iowa"               => "iowa",
			"Iowa State"        => "iowa-state",    "Kansas"             => "kansas",
			"Kansas State"      => "kansas-state",  "Kentucky"           => "kentucky",
			"LSU"               => "lsu",           "Maryland"           => "maryland",
			"Miami"             => "miami",         "Michigan"           => "michigan",
			"Michigan State"    => "michigan-state","Minnesota"          => "minnesota",
			"Mississippi State" => "mississippi-state", "Missouri"       => "missouri",
			"Nebraska"          => "nebraska",      "Nevada"             => "nevada",
			"North Carolina"    => "north-carolina","NC State"           => "nc-state",
			"Northwestern"      => "northwestern",  "Notre Dame"         => "notre-dame",
			"Ohio State"        => "ohio-state",    "Oklahoma"           => "oklahoma",
			"Oklahoma State"    => "oklahoma-state","Ole Miss"           => "ole-miss",
			"Oregon"            => "oregon",        "Oregon State"       => "oregon-state",
			"Penn State"        => "penn-state",    "Pitt"               => "pittsburgh",
			"Purdue"            => "purdue",        "South Carolina"     => "south-carolina",
			"Southern California" => "usc",         "Stanford"           => "stanford",
			"TCU"               => "tcu",           "Tennessee"          => "tennessee",
			"Texas"             => "texas",         "Texas A&M"          => "texas-am",
			"Texas Tech"        => "texas-tech",    "UCLA"               => "ucla",
			"Utah"              => "utah",          "Vanderbilt"         => "vanderbilt",
			"Virginia"          => "virginia",      "Virginia Tech"      => "virginia-tech",
			"Wake Forest"       => "wake-forest",   "Washington"         => "washington",
			"Washington State"  => "washington-state", "West Virginia"   => "west-virginia",
			"Wisconsin"         => "wisconsin",
			_ => string.Empty
		};
"""

# Insert helpers before the final closing braces of the class/namespace
# Find the last private static string method before end of class
insert_before = "    }\r\n}\r\n"
if src.endswith(insert_before):
	src = src[:-len(insert_before)] + HELPERS.replace("\n", "\r\n") + insert_before
	print("OK: helpers inserted")
else:
	# Try alternative endings
	alt = "    }\n}\n"
	if src.endswith(alt):
		src = src[:-len(alt)] + HELPERS + alt
		print("OK: helpers inserted (LF)")
	else:
		# Just append before last closing brace
		idx = src.rfind("\n    }\n}")
		if idx >= 0:
			src = src[:idx] + "\n" + HELPERS + src[idx:]
			print("OK: helpers appended at rfind")
		else:
			print("MISS: could not find class end")

with open(SVC, "w", encoding="utf-8") as fh:
	fh.write(src)

print("Done.")
