export function initNav(toggleBtnId = 'navbar-toggle', linksId = 'navbar-links') {
  const toggleBtn = document.getElementById(toggleBtnId);
  const links = document.getElementById(linksId);
  if (!toggleBtn || !links) return;

  toggleBtn.addEventListener('click', function () {
    links.classList.toggle('show');
    toggleBtn.classList.toggle('open');
  });
  document.addEventListener('click', function (e) {
    if (!toggleBtn.contains(e.target) && !links.contains(e.target)) {
      links.classList.remove('show');
      toggleBtn.classList.remove('open');
    }
  });
}
