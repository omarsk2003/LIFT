"use strict";

(() => {
  const icons = {
    overview: '<rect x="3" y="3" width="7" height="7" rx="1"/><rect x="14" y="3" width="7" height="7" rx="1"/><rect x="3" y="14" width="7" height="7" rx="1"/><rect x="14" y="14" width="7" height="7" rx="1"/>',
    clients: '<circle cx="9" cy="8" r="3"/><path d="M3 20v-2a6 6 0 0 1 12 0v2"/><path d="M17 5a3 3 0 0 1 0 6M17 14a5 5 0 0 1 4 5v1"/>',
    specialists: '<path d="M12 21s8-4 8-10V5l-8-3-8 3v6c0 6 8 10 8 10Z"/><path d="M9 11h6m-3-3v6"/>',
    admins: '<circle cx="12" cy="8" r="4"/><path d="M4 21v-2a8 8 0 0 1 16 0v2M18 4l1-2 1 2"/>',
    changes: '<path d="M4 7h15m-4-4 4 4-4 4M20 17H5m4-4-4 4 4 4"/>',
    subscriptions: '<rect x="3" y="5" width="18" height="14" rx="2"/><path d="M3 10h18M7 15h4"/>',
    messages: '<rect x="3" y="5" width="18" height="14" rx="2"/><path d="m4 7 8 6 8-6"/>',
    appearance: '<path d="M12 3a9 9 0 1 0 0 18h1a2 2 0 0 0 2-2c0-.6-.3-1.1-.7-1.5-.5-.4-.3-1.5.7-1.5h2a4 4 0 0 0 4-4 9 9 0 0 0-9-9Z"/><circle cx="7.5" cy="11" r=".7"/><circle cx="10" cy="7" r=".7"/><circle cx="15" cy="7" r=".7"/>',
    profile: '<circle cx="12" cy="8" r="4"/><path d="M4 21v-2a8 8 0 0 1 16 0v2"/>',
    chevron: '<path d="m9 18 6-6-6-6"/>'
  };
  const icon = key => `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${icons[key]}</svg>`;
  const items = [
    { group: "WORKSPACE", links: [["/Admin", "Overview", "overview"]] },
    { group: "PEOPLE & ACCESS", links: [["/Admin/Clients", "Clients", "clients"], ["/Admin/Specialists", "Coaches & nutritionists", "specialists"], ["/Admin/AdminAccounts", "Admins", "admins"], ["/Admin/AdminChanges", "Change requests", "changes"]] },
    { group: "PLATFORM", links: [["/Admin/Subscriptions", "Subscriptions", "subscriptions"], ["/Admin/Support", "Messages", "messages"], ["/Admin/SiteAppearance", "Site appearance", "appearance"]] },
    { group: "ACCOUNT", links: [["/Admin/Profile", "My profile", "profile"]] }
  ];

  class LiftAdminSidebar extends HTMLElement {
    connectedCallback() {
      const path = location.pathname.toLowerCase();
      const current = path.includes("/profile") ? "profile"
        : path.includes("/clients") ? "clients"
        : path.includes("/specialists") ? "specialists"
        : path.includes("/adminaccounts") ? "admins"
        : path.includes("/adminchanges") ? "changes"
        : path.includes("/subscriptions") ? "subscriptions"
        : path.includes("/support") ? "messages"
        : path.includes("/siteappearance") ? "appearance"
        : path.includes("/review") ? "specialists"
        : location.hash === "#accounts" ? "clients"
        : location.hash === "#specialists" ? "specialists" : "overview";
      const item = ([href, label, key]) => `<a class="lift-side-link${current === key ? " current" : ""}" href="${href}" title="${label}" aria-label="${label}"${current === key ? ' aria-current="page"' : ""}>${icon(key)}<span class="lift-side-label">${label}</span></a>`;
      this.innerHTML = `<aside class="admin-sidebar" id="admin-sidebar" aria-label="Admin navigation"><nav aria-label="Admin sections">${items.map(group => `<div class="lift-side-group"><p class="lift-side-heading">${group.group}</p>${group.links.map(item).join("")}</div>`).join("")}</nav><button class="lift-side-collapse" type="button" aria-label="Expand sidebar" aria-expanded="false" title="Expand sidebar">${icon("chevron")}<span class="lift-side-label">Collapse menu</span></button></aside>`;
      const shell = this.closest(".admin-shell");
      const button = this.querySelector(".lift-side-collapse");
      const setExpanded = value => {
        this.dataset.expanded = String(value);
        shell?.classList.toggle("sidebar-expanded", value);
        button?.setAttribute("aria-expanded", String(value));
        button?.setAttribute("aria-label", value ? "Collapse sidebar" : "Expand sidebar");
        button?.setAttribute("title", value ? "Collapse sidebar" : "Expand sidebar");
      };
      const wideLayout = window.matchMedia("(min-width: 1050px)");
      setExpanded(false);
      wideLayout.addEventListener("change", () => setExpanded(false));
      this.addEventListener("mouseenter", () => { if (wideLayout.matches) setExpanded(true); });
      this.addEventListener("mouseleave", () => { if (wideLayout.matches) setExpanded(false); });
      button?.addEventListener("click", () => { if (!wideLayout.matches) setExpanded(this.dataset.expanded !== "true"); });
      document.querySelector(".admin-menu-toggle")?.addEventListener("click", () => { if (!wideLayout.matches) setExpanded(this.dataset.expanded !== "true"); });
      this.querySelectorAll(".lift-side-link").forEach(link => link.addEventListener("click", () => {
        if (location.pathname.toLowerCase() === "/admin" && link.hash) {
          this.querySelectorAll(".lift-side-link").forEach(item => { item.classList.remove("current"); item.removeAttribute("aria-current"); });
          link.classList.add("current"); link.setAttribute("aria-current", "page");
        }
        this.classList.remove("open");
        if (!wideLayout.matches) setExpanded(false);
        document.querySelector(".admin-menu-toggle")?.setAttribute("aria-expanded", "false");
      }));
    }
  }
  if (!customElements.get("lift-admin-sidebar")) customElements.define("lift-admin-sidebar", LiftAdminSidebar);
})();
