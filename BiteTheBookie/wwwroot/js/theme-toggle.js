(function () {
  var KEY = 'btb-theme';
  var root = document.documentElement;

  function isCash() {
    return root.classList.contains('theme-cash');
  }

  function updateButton(btn) {
    if (!btn) return;
    var label = btn.querySelector('.theme-toggle-label');
    var icon = btn.querySelector('i');
    if (isCash()) {
      if (label) label.textContent = 'Light';
      if (icon) icon.className = 'fas fa-sun me-1';
      btn.title = 'Switch to light theme';
    } else {
      if (label) label.textContent = 'Dark';
      if (icon) icon.className = 'fas fa-moon me-1';
      btn.title = 'Switch to dark theme';
    }
  }

  function setTheme(cash) {
    root.classList.toggle('theme-cash', cash);
    try { localStorage.setItem(KEY, cash ? 'cash' : 'light'); } catch (e) {}
    updateButton(document.getElementById('themeToggleBtn'));
  }

  document.addEventListener('DOMContentLoaded', function () {
    var btn = document.getElementById('themeToggleBtn');
    updateButton(btn);
    if (btn) {
      btn.addEventListener('click', function () {
        setTheme(!isCash());
      });
    }
  });
})();
