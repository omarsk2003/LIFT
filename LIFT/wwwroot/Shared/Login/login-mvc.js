"use strict";

// Static preview accounts only. These are not real credentials or authentication.
// Never store real passwords in frontend code. Backend authentication comes later.
const previewAccounts = [
  { email: "client@lift.demo", password: "Lift2026!", page: "../../Client/Dashboard/dashboard.html", plan: "premium", name: "Alexander Drake" },
  { email: "client.free@lift.demo", password: "Lift2026!", page: "../../Client/Dashboard/dashboard.html", plan: "free", name: "Free Client" },
  { email: "client.ai@lift.demo", password: "Lift2026!", page: "../../Client/Dashboard/dashboard.html", plan: "ai", name: "AI Plan Client" },
  { email: "coach@lift.demo", password: "Lift2026!", page: "../../Coach/Dashboard/dashboard.html" },
  { email: "nutritionist@lift.demo", password: "Lift2026!", page: "../../Nutrition/Dashboard/dashboard.html" },
  { email: "admin@lift.demo", password: "Lift2026!", page: "../../Admin/Dashboard/dashboard.html" }
];

const form = document.getElementById("login-form");
const email = document.getElementById("email");
const password = document.getElementById("password");
const passwordToggle = document.getElementById("toggle-password");
const message = document.getElementById("form-message");
const submitButton = document.getElementById("sign-in");
const status = document.getElementById("sign-in-status");
const recoveryDialog = document.getElementById("recovery-dialog");
const journeyLine = document.querySelector(".route-line");
const journeyMarker = document.querySelector(".route-marker");
const journeyStage = document.getElementById("journey-stage");
let redirectTimer;

function updateJourney(stage) {
  const steps = {
    ready: { progress: 0, x: 0, y: 0, label: "READY TO CONTINUE" },
    email: { progress: 24, x: 150, y: -71, label: "FINDING YOUR PATH" },
    emailValid: { progress: 35, x: 210, y: -49, label: "FIRST STEP COMPLETE" },
    password: { progress: 52, x: 299, y: -11, label: "KEEP MOVING" },
    valid: { progress: 78, x: 466, y: -81, label: "NEXT STEP READY" },
    error: { progress: 24, x: 150, y: -71, label: "TAKE ANOTHER STEP" },
    success: { progress: 100, x: 571, y: -121, label: "CHECKPOINT REACHED" }
  };
  const next = steps[stage];
  journeyLine.style.strokeDasharray = `${next.progress} 100`;
  journeyMarker.style.transform = `translate(${next.x}px, ${next.y}px)`;
  journeyStage.textContent = next.label;
}

function syncJourney() {
  const validEmail = email.value.trim() && email.validity.valid;
  updateJourney(validEmail && password.value ? "valid" : password.value ? "password" : validEmail ? "emailValid" : email.value.trim() ? "email" : "ready");
}

function setError(input, text) {
  document.getElementById(input.id + "-error").textContent = text;
  input.setAttribute("aria-invalid", String(Boolean(text)));
}

function clearMessage() {
  message.hidden = true;
  message.textContent = "";
}

[email, password].forEach(input => {
  input.addEventListener("focus", () => updateJourney(input === email ? "email" : "password"));
  input.addEventListener("input", () => {
    setError(input, "");
    clearMessage();
    syncJourney();
  });
});

passwordToggle.addEventListener("click", () => {
  const visible = password.type === "password";
  password.type = visible ? "text" : "password";
  passwordToggle.setAttribute("aria-label", visible ? "Hide password" : "Show password");
  passwordToggle.setAttribute("aria-pressed", String(visible));
});

form.addEventListener("submit", event => {
  event.preventDefault();
  if (submitButton.disabled) return;
  clearMessage();
  email.value = email.value.trim();
  const emailError = !email.value ? "Enter your email address." : email.validity.typeMismatch ? "Enter a valid email address." : "";
  const passwordError = !password.value ? "Enter your password." : "";
  setError(email, emailError); setError(password, passwordError);
  if (emailError || passwordError) { (emailError ? email : password).focus(); updateJourney("error"); return; }
  updateJourney("success");
  submitButton.disabled = true;
  submitButton.querySelector(".button-arrow").hidden = true;
  submitButton.querySelector(".spinner").hidden = false;
  submitButton.querySelector("span").textContent = "Opening your dashboard";
  status.textContent = "Checking your account…";
  redirectTimer = window.setTimeout(() => form.submit(), 650);
});

document.getElementById("forgot-password").addEventListener("click", () => recoveryDialog.showModal());
document.getElementById("close-recovery").addEventListener("click", () => recoveryDialog.close());
recoveryDialog.addEventListener("click", event => {
  const rect = recoveryDialog.getBoundingClientRect();
  if (event.clientX < rect.left || event.clientX > rect.right || event.clientY < rect.top || event.clientY > rect.bottom) recoveryDialog.close();
});
// Restore the form when navigating back from a preview dashboard.
window.addEventListener("pageshow", () => {
  clearTimeout(redirectTimer);
  submitButton.disabled = false;
  submitButton.querySelector(".button-arrow").hidden = false;
  submitButton.querySelector(".spinner").hidden = true;
  submitButton.querySelector("span").textContent = "Sign in";
  status.textContent = "";
  syncJourney();
});
const accountState = new URLSearchParams(location.search).get("account");
if (["suspended", "blocked", "rejected"].includes(accountState)) {
  message.textContent = accountState === "suspended" ? "This account is suspended. Contact LIFT Admin." : accountState === "blocked" ? "This account is blocked. Contact LIFT Admin." : "This account was not approved.";
  message.hidden = false;
}


