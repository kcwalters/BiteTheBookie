// Toggle BetSlip visibility
function removeBet(gameId) {
    fetch('/BetSlip/Remove', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ gameId })
    }).then(() => location.reload());
}

// Refresh NFL ticker every 60 seconds
(function () {
    const REFRESH_MS = 60000; // 60 seconds
    async function refreshTicker() {
        try {
            const response = await fetch('/Scores/NflTickerInner');
            if (!response.ok) return;
            const html = await response.text();
            const inner = document.querySelector('#nfl-ticker .nfl-ticker-inner');
            if (inner) inner.innerHTML = html;
        } catch (e) {
            console.error('NFL ticker refresh failed', e);
        }
    }
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => {
            setInterval(refreshTicker, REFRESH_MS);
        });
    } else {
        setInterval(refreshTicker, REFRESH_MS);
    }
})();