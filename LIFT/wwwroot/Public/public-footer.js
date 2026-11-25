"use strict";
(() => {
  class LiftFooter extends HTMLElement {
    connectedCallback() {
      this.innerHTML = `<footer class="public-footer"><div class="footer-main"><div class="footer-brand-block"><a class="footer-brand" href="/Public/Landing#top" aria-label="LIFT home">LIFT<span>.</span></a><p>A healthier life.<br>A rhythm that’s yours.</p></div><div class="footer-column"><h2>EXPLORE</h2><a href="/Public/HowItWorks">How it works</a><a href="/Public/Landing#plans">Plans</a><a href="/Public/Landing#feedback">Community stories</a><a href="/Public/About">About LIFT</a></div><div class="footer-column"><h2>SUPPORT</h2><a href="/Public/About#faq">Common questions</a><a href="/Public/Contact">Contact us</a><a href="/Account/Register">Start your journey</a></div><div class="footer-column"><h2>WORK WITH LIFT</h2><a href="/CoachRegistration/Apply">Become a coach</a><a href="/NutritionistRegistration/Apply">Become a nutritionist</a></div></div><div class="footer-bottom"><span>© ${new Date().getFullYear()} LIFT. All rights reserved.</span><span>LIFESTYLE &amp; INTELLIGENT FITNESS TECHNOLOGY</span><a href="/Public/Landing#top">Back to top ↑</a></div></footer>`;
    }
  }
  customElements.define("lift-footer", LiftFooter);
})();
