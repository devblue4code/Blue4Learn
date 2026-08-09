document.addEventListener("DOMContentLoaded", () => {
  if (window.hljs) {
    document.querySelectorAll(".markdown-body pre code").forEach((block) => {
      window.hljs.highlightElement(block);
    });
  }

  const shell = document.querySelector("[data-app-shell]");
  if (shell) {
    document.querySelectorAll("[data-app-nav-toggle]").forEach((btn) => {
      btn.addEventListener("click", () => shell.classList.toggle("is-nav-open"));
    });
    document.querySelectorAll("[data-app-nav-close]").forEach((el) => {
      el.addEventListener("click", () => shell.classList.remove("is-nav-open"));
    });
  }

  document.querySelectorAll("[data-copy-code]").forEach((btn) => {
    btn.addEventListener("click", async () => {
      const value = btn.getAttribute("data-copy-code");
      if (!value) return;
      try {
        await navigator.clipboard.writeText(value);
        btn.setAttribute("title", "Copiado");
      } catch {
        /* ignore */
      }
    });
  });
});
