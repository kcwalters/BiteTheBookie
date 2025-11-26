// Toggle BetSlip visibility
function removeBet(gameId) {
    fetch('/BetSlip/Remove', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ gameId })
    }).then(() => location.reload());
}

// Add any carousel init or SignalR hookup here
