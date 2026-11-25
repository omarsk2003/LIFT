"use strict";
(() => {
  const landing = "/Public/Landing";
  const how = "/Public/HowItWorks";
  const about = "/Public/About";
  const contact = "/Public/Contact";
  class LiftNavbar extends HTMLElement {
    connectedCallback() {
      const onLanding = location.pathname.toLowerCase() === landing.toLowerCase();
      this.innerHTML = `<header class="masthead"><a class="logo" href="${landing}#top" aria-label="LIFT home">LIFT<span>.</span></a><nav id="public-navigation" aria-label="Main navigation"><a href="${landing}#story">The story</a><a href="${how}">How it works</a><a href="${about}">About LIFT</a><a href="${contact}">Contact</a></nav><div class="nav-actions"><button class="landing-theme-toggle" type="button" data-theme-toggle><span data-theme-icon aria-hidden="true"></span></button><a class="nav-cta" href="/Account/Register">Get started <span aria-hidden="true">↗</span></a><button class="public-menu-toggle" type="button" aria-expanded="false" aria-controls="public-navigation" aria-label="Open menu"><span></span></button></div></header>`;
      const header = this.querySelector(".masthead");
      const nav = this.querySelector("nav");
      const toggle = this.querySelector(".public-menu-toggle");
      const updateCurrent = () => this.querySelectorAll("nav a").forEach(link => {
        const target = new URL(link.href);
        const current = target.pathname.toLowerCase() === location.pathname.toLowerCase() && (target.hash ? target.hash === location.hash : !location.hash);
        if (current) link.setAttribute("aria-current", target.hash ? "location" : "page");
        else link.removeAttribute("aria-current");
      });
      updateCurrent();
      window.addEventListener("hashchange", updateCurrent);
      const close = () => { toggle.setAttribute("aria-expanded", "false"); toggle.setAttribute("aria-label", "Open menu"); nav.classList.remove("open"); };
      toggle.addEventListener("click", () => {
        const open = toggle.getAttribute("aria-expanded") !== "true";
        toggle.setAttribute("aria-expanded", String(open));
        toggle.setAttribute("aria-label", open ? "Close menu" : "Open menu");
        nav.classList.toggle("open", open);
      });
      nav.addEventListener("click", event => { if (event.target.closest("a")) close(); });
      this.addEventListener("keydown", event => { if (event.key === "Escape") { close(); toggle.focus(); } });
      if (!onLanding) header.dataset.darkScene = "false";
    }
  }
  customElements.define("lift-navbar", LiftNavbar);
})();
