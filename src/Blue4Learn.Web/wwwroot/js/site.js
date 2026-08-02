document.addEventListener("DOMContentLoaded", () => {
  if (window.hljs) {
    document.querySelectorAll(".markdown-body pre code").forEach((block) => {
      window.hljs.highlightElement(block);
    });
  }
});
