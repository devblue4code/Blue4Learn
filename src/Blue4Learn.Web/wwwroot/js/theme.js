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
    const buttons = document.querySelectorAll("[data-theme-toggle]");
    buttons.forEach((btn) => {
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

  // Apply ASAP to reduce flash when preference is stored.
  applyTheme(currentTheme());

  function createToggle(extraClass) {
    const btn = document.createElement("button");
    btn.type = "button";
    btn.className = extraClass ? `theme-toggle ${extraClass}` : "theme-toggle";
    btn.setAttribute("data-theme-toggle", "");
    btn.innerHTML = sunIcon + moonIcon;
    btn.addEventListener("click", toggleTheme);
    return btn;
  }

  function mountToggle() {
    if (document.querySelector("[data-theme-toggle]")) {
      applyTheme(currentTheme());
      return;
    }

    const topActions = document.querySelector(".app-top-actions");
    if (topActions) {
      topActions.prepend(createToggle());
      applyTheme(currentTheme());
      return;
    }

    const account = document.querySelector(".nav-account");
    if (account) {
      account.prepend(createToggle());
      applyTheme(currentTheme());
      return;
    }

    if (document.body.classList.contains("auth-body")) {
      document.body.appendChild(createToggle("theme-toggle-auth"));
      applyTheme(currentTheme());
    }
  }

  window.Blue4Learn = window.Blue4Learn || {};
  window.Blue4Learn.setTheme = (theme) => {
    if (theme !== "light" && theme !== "dark") return;
    localStorage.setItem(KEY, theme);
    applyTheme(theme);
  };
  window.Blue4Learn.toggleTheme = toggleTheme;

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", mountToggle);
  } else {
    mountToggle();
  }

  window.matchMedia("(prefers-color-scheme: dark)").addEventListener("change", () => {
    if (!localStorage.getItem(KEY)) applyTheme(systemTheme());
  });
})();
