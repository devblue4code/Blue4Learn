(() => {
  const KEY = "b4l-theme";
  const sunIcon = `<svg class="theme-icon-sun" viewBox="0 0 24 24" width="18" height="18" aria-hidden="true" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><circle cx="12" cy="12" r="4"/><path d="M12 2v2M12 20v2M4.93 4.93l1.41 1.41M17.66 17.66l1.41 1.41M2 12h2M20 12h2M4.93 19.07l1.41-1.41M17.66 6.34l1.41-1.41"/></svg>`;
  const moonIcon = `<svg class="theme-icon-moon" viewBox="0 0 24 24" width="18" height="18" aria-hidden="true" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><path d="M21 14.5A8.5 8.5 0 0 1 9.5 3 7 7 0 1 0 21 14.5z"/></svg>`;

  function systemTheme() {
    return window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
  }

  function currentTheme() {
    const saved = localStorage.getItem(KEY);
    if (saved === "light" || saved === "dark") return saved;
    return systemTheme();
  }

  function applyTheme(theme) {
    document.documentElement.setAttribute("data-theme", theme);
    document.querySelectorAll("[data-theme-toggle]").forEach((btn) => {
      const next = theme === "dark" ? "claro" : "escuro";
      btn.setAttribute("aria-label", `Ativar tema ${next}`);
      btn.title = `Tema ${next}`;
    });
  }

  function toggleTheme() {
    const next = currentTheme() === "dark" ? "light" : "dark";
    localStorage.setItem(KEY, next);
    applyTheme(next);
  }

  applyTheme(currentTheme());

  function mountToggle() {
    if (!document.querySelector("[data-theme-toggle]")) {
      const btn = document.createElement("button");
      btn.type = "button";
      btn.className = "theme-toggle theme-toggle-auth";
      btn.setAttribute("data-theme-toggle", "");
      btn.innerHTML = sunIcon + moonIcon;
      btn.addEventListener("click", toggleTheme);
      document.body.appendChild(btn);
    }
    applyTheme(currentTheme());
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", mountToggle);
  } else {
    mountToggle();
  }
})();
