(() => {
  const modalEl = document.getElementById('nflTeamModal');
  if (!modalEl) {
    return;
  }

  const modal = bootstrap.Modal.getOrCreateInstance(modalEl);

  // Close when the mouse exits the modal content (and its children).
  modalEl.addEventListener('mouseleave', () => {
    modal.hide();
  });
})();