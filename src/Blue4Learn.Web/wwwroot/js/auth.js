document.addEventListener("DOMContentLoaded", () => {
  document.querySelectorAll("[data-fill-login]").forEach((button) => {
    button.addEventListener("click", () => {
      const email = button.getAttribute("data-email");
      const password = button.getAttribute("data-password");
      const emailInput = document.getElementById("Input_Email");
      const passwordInput = document.getElementById("Input_Password");
      if (emailInput && email) emailInput.value = email;
      if (passwordInput && password) {
        passwordInput.value = password;
        passwordInput.focus();
      }
    });
  });
});
