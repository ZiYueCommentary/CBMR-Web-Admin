document.addEventListener("click", event => {
    const button = event.target.closest("button[data-password-toggle]");
    if (!button || button.disabled) return;

    const input = document.getElementById(button.dataset.passwordToggle);
    if (!input || input.disabled) return;

    const show = input.type === "password";
    input.type = show ? "text" : "password";
    const label = show ? button.dataset.hideLabel : button.dataset.showLabel;
    button.setAttribute("aria-label", label);
    button.title = label;
    button.querySelector("i").className = show ? "bi bi-eye-slash" : "bi bi-eye";
});
