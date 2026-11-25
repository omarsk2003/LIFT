"use strict";
document.addEventListener("DOMContentLoaded", () => {
  const menus = [...document.querySelectorAll("[data-admin-menu]")];
  const close = () => menus.forEach(button => {
    button.setAttribute("aria-expanded", "false");
    document.getElementById(button.getAttribute("aria-controls"))?.setAttribute("hidden", "");
  });
  menus.forEach(button => button.addEventListener("click", event => {
    event.stopPropagation();
    const open = button.getAttribute("aria-expanded") !== "true";
    close();
    if (open) {
      button.setAttribute("aria-expanded", "true");
      document.getElementById(button.getAttribute("aria-controls"))?.removeAttribute("hidden");
    }
  }));
  document.addEventListener("click", event => { if (!event.target.closest(".admin-header-popover")) close(); });
  document.addEventListener("keydown", event => { if (event.key === "Escape") close(); });
  document.querySelectorAll("[data-admin-dialog-open]").forEach(button => button.addEventListener("click", () => {
    const dialog = document.getElementById(button.dataset.adminDialogOpen);
    if (dialog instanceof HTMLDialogElement) { dialog.showModal(); dialog.querySelector("input:not([type=hidden])")?.focus(); }
  }));
  document.querySelectorAll("[data-admin-dialog-close]").forEach(button => button.addEventListener("click", () => button.closest("dialog")?.close()));
  document.querySelectorAll("[data-growth-period]").forEach(button => button.addEventListener("click", () => {
    const chart = button.closest("[data-growth-chart]");
    const months = Number(button.dataset.growthPeriod);
    chart?.querySelectorAll("[data-growth-month]").forEach((item, index, all) => { item.hidden = index < all.length - months; });
    chart?.querySelectorAll("[data-growth-period]").forEach(item => item.setAttribute("aria-pressed", String(item === button)));
  }));
  document.querySelector('[data-growth-period="6"]')?.click();
  const applyDirectoryFilters = () => {
    const status = document.getElementById("directory-filter")?.value || "all";
    const role = document.getElementById("role-filter")?.value || "all";
    document.querySelectorAll(".admin-data-table tbody tr[data-status]").forEach(row => {
      row.hidden = (status !== "all" && row.dataset.status !== status) || (role !== "all" && row.dataset.role !== role);
    });
  };
  document.querySelectorAll("#directory-filter, #role-filter").forEach(control => control.addEventListener("change", applyDirectoryFilters));
  document.querySelectorAll("[data-review-url]").forEach(button => button.addEventListener("click", () => {
    const dialog = document.getElementById("review-popup");
    const frame = dialog?.querySelector("iframe");
    if (frame) { frame.src = button.dataset.reviewUrl; dialog.showModal(); }
  }));
  document.getElementById("review-popup")?.addEventListener("close", event => { event.target.querySelector("iframe").src = "about:blank"; });
  const promoList = document.getElementById("promo-list");
  if (promoList) {
    const rows = [...promoList.querySelectorAll(".promo-row")];
    const applyPromoFilters = () => {
      const query = document.getElementById("promo-search").value.trim().toLowerCase();
      const plan = document.getElementById("promo-plan-filter").value;
      const direction = document.getElementById("promo-order").value === "oldest" ? 1 : -1;
      const ordered = [...rows].sort((a, b) => direction * (Number(a.dataset.promoStart) - Number(b.dataset.promoStart)));
      ordered.forEach(row => { row.hidden = !row.dataset.promoCode.toLowerCase().includes(query) || (plan !== "all" && row.dataset.promoPlan !== plan); promoList.append(row); });
    };
    document.querySelectorAll("#promo-search, #promo-order, #promo-plan-filter").forEach(control => control.addEventListener(control.id === "promo-search" ? "input" : "change", applyPromoFilters));
  }
  document.getElementById("revenue-month")?.addEventListener("change", event => {
    document.querySelectorAll("[data-revenue-month]").forEach(month => { month.hidden = month.dataset.revenueMonth !== event.target.value; });
  });
});
