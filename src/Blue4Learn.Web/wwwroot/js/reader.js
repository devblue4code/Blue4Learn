(function () {
  const root = document.querySelector("[data-reader]");
  if (!root) return;

  const content = root.querySelector("[data-reader-content]");
  const tocNav = root.querySelector("[data-reader-toc-nav]");
  const tocEmpty = root.querySelector("[data-reader-toc-empty]");
  const main = root.querySelector("[data-reader-main]");
  const tocToggle = root.querySelector("[data-reader-toc-toggle]");
  const focusBtn = root.querySelector("[data-reader-focus]");
  const closeButtons = root.querySelectorAll("[data-reader-toc-close]");

  const storageKey = "b4l-reader";
  const mqMobile = window.matchMedia("(max-width: 900px)");

  function loadPrefs() {
    try {
      return JSON.parse(localStorage.getItem(storageKey) || "{}");
    } catch {
      return {};
    }
  }

  function savePrefs(partial) {
    try {
      localStorage.setItem(storageKey, JSON.stringify({ ...loadPrefs(), ...partial }));
    } catch { /* ignore */ }
  }

  function slugify(text) {
    return text
      .toLowerCase()
      .normalize("NFD")
      .replace(/[\u0300-\u036f]/g, "")
      .replace(/[^a-z0-9]+/g, "-")
      .replace(/^-+|-+$/g, "")
      .slice(0, 80) || "secao";
  }

  function ensureHeadingIds(headings) {
    const used = new Set();
    headings.forEach((h) => {
      let id = h.id || slugify(h.textContent || "secao");
      let base = id;
      let n = 2;
      while (used.has(id) || (document.getElementById(id) && document.getElementById(id) !== h)) {
        id = `${base}-${n++}`;
      }
      h.id = id;
      used.add(id);
    });
  }

  function buildToc() {
    if (!content || !tocNav) return [];
    const headings = Array.from(content.querySelectorAll("h1, h2, h3"));
    ensureHeadingIds(headings);

    tocNav.querySelectorAll(".reader-toc-item").forEach((el) => el.remove());

    if (headings.length === 0) {
      if (tocEmpty) tocEmpty.hidden = false;
      return [];
    }

    if (tocEmpty) tocEmpty.hidden = true;

    headings.forEach((h) => {
      const level = Number(h.tagName.substring(1));
      const a = document.createElement("a");
      a.href = `#${h.id}`;
      a.className = `reader-toc-item level-${level}`;
      a.textContent = (h.textContent || "").trim();
      a.dataset.target = h.id;
      a.addEventListener("click", (e) => {
        e.preventDefault();
        const target = document.getElementById(h.id);
        if (!target || !main) return;
        const top = target.offsetTop - 12;
        main.scrollTo({ top, behavior: "smooth" });
        history.replaceState(null, "", `#${h.id}`);
        if (mqMobile.matches) setTocOpen(false);
      });
      tocNav.appendChild(a);
    });

    return headings;
  }

  function setActive(id) {
    tocNav?.querySelectorAll(".reader-toc-item").forEach((el) => {
      el.classList.toggle("is-active", el.dataset.target === id);
    });
  }

  function observeHeadings(headings) {
    if (!main || headings.length === 0) return;
    const observer = new IntersectionObserver(
      (entries) => {
        const visible = entries
          .filter((e) => e.isIntersecting)
          .sort((a, b) => a.boundingClientRect.top - b.boundingClientRect.top);
        if (visible[0]?.target?.id) setActive(visible[0].target.id);
      },
      { root: main, rootMargin: "-10% 0px -70% 0px", threshold: [0, 1] }
    );
    headings.forEach((h) => observer.observe(h));
  }

  function setTocOpen(open) {
    root.classList.toggle("is-toc-open", open);
    if (!mqMobile.matches) {
      root.classList.toggle("is-toc-collapsed", !open);
      savePrefs({ tocCollapsed: !open });
    }
    tocToggle?.setAttribute("aria-expanded", String(open));
    tocToggle?.classList.toggle("is-active", open && mqMobile.matches ? open : !root.classList.contains("is-toc-collapsed"));
  }

  function setFocus(on) {
    root.classList.toggle("is-focus", on);
    focusBtn?.setAttribute("aria-pressed", String(on));
    focusBtn?.classList.toggle("is-active", on);
    const icon = focusBtn?.querySelector("i");
    if (icon) {
      icon.className = on ? "bi bi-fullscreen-exit" : "bi bi-arrows-fullscreen";
    }
    savePrefs({ focus: on });
    if (on) setTocOpen(false);
  }

  const prefs = loadPrefs();
  const headings = buildToc();
  observeHeadings(headings);

  if (prefs.focus) {
    setFocus(true);
  } else if (mqMobile.matches) {
    setTocOpen(false);
  } else {
    setTocOpen(!prefs.tocCollapsed);
  }

  tocToggle?.addEventListener("click", () => {
    if (root.classList.contains("is-focus")) setFocus(false);
    const open = mqMobile.matches
      ? !root.classList.contains("is-toc-open")
      : root.classList.contains("is-toc-collapsed");
    setTocOpen(open);
  });

  closeButtons.forEach((btn) => btn.addEventListener("click", () => setTocOpen(false)));

  focusBtn?.addEventListener("click", () => {
    setFocus(!root.classList.contains("is-focus"));
  });

  document.addEventListener("keydown", (e) => {
    if (e.key === "Escape") {
      if (root.classList.contains("is-focus")) setFocus(false);
      else if (root.classList.contains("is-toc-open")) setTocOpen(false);
    }
  });

  if (location.hash) {
    const el = document.getElementById(location.hash.slice(1));
    if (el && main) {
      requestAnimationFrame(() => {
        main.scrollTo({ top: el.offsetTop - 12 });
        setActive(el.id);
      });
    }
  }
})();
