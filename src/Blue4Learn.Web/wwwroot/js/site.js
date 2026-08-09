(() => {
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
})();

(() => {
  const aliasMap = {
    "language-cs": "language-csharp",
    "language-c#": "language-csharp",
    "language-c-sharp": "language-csharp",
    "language-razor": "language-cshtml-razor",
    "language-cshtml": "language-cshtml-razor",
    "language-razor-cshtml": "language-cshtml-razor"
  };

  const copyIcon = `<svg viewBox="0 0 16 16" width="16" height="16" aria-hidden="true"><path fill="currentColor" d="M0 6.75C0 5.784.784 5 1.75 5h1.5a.75.75 0 0 1 0 1.5h-1.5a.25.25 0 0 0-.25.25v7.5c0 .138.112.25.25.25h7.5a.25.25 0 0 0 .25-.25v-1.5a.75.75 0 0 1 1.5 0v1.5A1.75 1.75 0 0 1 9.25 16h-7.5A1.75 1.75 0 0 1 0 14.25Z"/><path fill="currentColor" d="M5 1.75C5 .784 5.784 0 6.75 0h7.5C15.216 0 16 .784 16 1.75v7.5A1.75 1.75 0 0 1 14.25 11h-7.5A1.75 1.75 0 0 1 5 9.25Zm1.75-.25a.25.25 0 0 0-.25.25v7.5c0 .138.112.25.25.25h7.5a.25.25 0 0 0 .25-.25v-7.5a.25.25 0 0 0-.25-.25Z"/></svg>`;
  const checkIcon = `<svg viewBox="0 0 16 16" width="16" height="16" aria-hidden="true"><path fill="currentColor" d="M13.78 4.22a.75.75 0 0 1 0 1.06l-7.25 7.25a.75.75 0 0 1-1.06 0L2.22 9.28a.751.751 0 0 1 .018-1.042.751.751 0 0 1 1.042-.018L6 10.94l6.72-6.72a.75.75 0 0 1 1.06 0Z"/></svg>`;

  async function copyText(text) {
    if (navigator.clipboard?.writeText) {
      await navigator.clipboard.writeText(text);
      return;
    }

    const ta = document.createElement("textarea");
    ta.value = text;
    ta.setAttribute("readonly", "");
    ta.style.position = "fixed";
    ta.style.left = "-9999px";
    document.body.appendChild(ta);
    ta.select();
    document.execCommand("copy");
    document.body.removeChild(ta);
  }

  function createCopyButton(getText) {
    const btn = document.createElement("button");
    btn.type = "button";
    btn.className = "code-copy-btn";
    btn.setAttribute("aria-label", "Copiar código");
    btn.title = "Copiar";
    btn.innerHTML = copyIcon;

    btn.addEventListener("click", async (e) => {
      e.preventDefault();
      e.stopPropagation();
      try {
        const text = (typeof getText === "function" ? getText() : getText) || "";
        await copyText(text.replace(/\n$/, ""));
        btn.classList.add("is-copied");
        btn.innerHTML = checkIcon;
        btn.title = "Copiado!";
        btn.setAttribute("aria-label", "Código copiado");
        setTimeout(() => {
          btn.classList.remove("is-copied");
          btn.innerHTML = copyIcon;
          btn.title = "Copiar";
          btn.setAttribute("aria-label", "Copiar código");
        }, 1800);
      } catch (err) {
        console.warn("Não foi possível copiar", err);
      }
    });

    return btn;
  }

  function highlightBlock(block) {
    Object.entries(aliasMap).forEach(([from, to]) => {
      if (block.classList.contains(from)) {
        block.classList.remove(from);
        block.classList.add(to);
      }
    });

    const hasLang = [...block.classList].some((c) => c.startsWith("language-"));
    if (!hasLang) {
      block.classList.add("language-csharp");
    }

    if (window.hljs) {
      try {
        window.hljs.highlightElement(block);
      } catch (err) {
        console.warn("Falha ao destacar código", err);
      }
    }
  }

  function enhancePre(pre) {
    if (pre.dataset.copyReady === "1") return;

    const code = pre.querySelector("code") || pre;
    highlightBlock(code instanceof HTMLElement && code.tagName === "CODE" ? code : pre);

    let wrap = pre.parentElement;
    if (!wrap?.classList.contains("code-block")) {
      wrap = document.createElement("div");
      wrap.className = "code-block";
      pre.parentNode.insertBefore(wrap, pre);
      wrap.appendChild(pre);
    }

    if (!wrap.querySelector(".code-copy-btn")) {
      wrap.appendChild(createCopyButton(() => code.innerText || code.textContent || ""));
    }

    pre.dataset.copyReady = "1";
  }

  function enhanceInlineCopyables(root) {
    root.querySelectorAll("[data-copy], .copyable-code").forEach((el) => {
      if (el.dataset.copyReady === "1") return;

      const text = el.getAttribute("data-copy") || el.textContent || "";
      let wrap = el.parentElement;
      if (!wrap?.classList.contains("copyable-code-wrap")) {
        wrap = document.createElement("span");
        wrap.className = "copyable-code-wrap";
        el.parentNode.insertBefore(wrap, el);
        wrap.appendChild(el);
      }

      if (!wrap.querySelector(".code-copy-btn")) {
        const btn = createCopyButton(text.trim());
        btn.classList.add("code-copy-btn-inline");
        wrap.appendChild(btn);
      }

      el.dataset.copyReady = "1";
    });
  }

  function enhanceCodeBlocks(root = document) {
    if (window.hljs) {
      try {
        window.hljs.configure({
          ignoreUnescapedHTML: true,
          languages: [
            "csharp",
            "cshtml-razor",
            "xml",
            "css",
            "javascript",
            "json",
            "sql",
            "bash",
            "plaintext"
          ]
        });
      } catch {
        // ignore
      }
    }

    root.querySelectorAll(".markdown-body pre").forEach(enhancePre);
    enhanceInlineCopyables(root);
  }

  window.Blue4Learn = window.Blue4Learn || {};
  window.Blue4Learn.enhanceCodeBlocks = enhanceCodeBlocks;

  document.addEventListener("DOMContentLoaded", () => enhanceCodeBlocks(document));
})();
