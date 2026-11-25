/**
 * LIFT Editorial Journey — Interactive Choreography & Motion Engine
 * macOS-Inspired Web Experience Implementation
 *
 * Sequence Flow:
 * 1. Hero: Heroine centered in living room.
 * 2. Approaching Phone & Input Streaming:
 *    - Camera pushes into the room.
 *    - Heroine holds phone.
 *    - Her inputs stream directly INTO the phone screen.
 * 3. Inside the Phone:
 *    - Real-time AI computation & synthesis takes place.
 *    - The adapted workout and nutrition plan appears INSIDE the phone screen.
 * 4. Coach 1-on-1 Synchronized Training (Dual Frame):
 *    - Coach Daniel demonstrates on the left frame.
 *    - The EXACT SAME Heroine mirrors the lunge on the right frame instantly (1:1 Rep Match).
 * 5. Kitchen Culinary Video Sync (Dual Frame):
 *    - Recipe masterclass video plays on the left frame.
 *    - The EXACT SAME Heroine cooks and prepares the meal in her kitchen on the right frame.
 * 6. Finale: Heroine returns gracefully to center with unified AI + Coach + Nutritionist closing.
 */

(() => {
  const journey = document.querySelector('.journey');
  const count = document.querySelector('.scene-count');
  const aiStatusText = document.querySelector('.ai-status-text');
  if (!journey) return;

  const reduced = matchMedia('(prefers-reduced-motion: reduce)');
  const root = document.documentElement;
  const header = document.querySelector('.masthead');
  const stage = document.querySelector('.stage');
  const followup = document.querySelector('#plans');
  let headerScrolled = scrollY >= 72;

  // macOS-style header elevation with hysteresis
  function updateHeader() {
    if (!headerScrolled && scrollY >= 72) {
      headerScrolled = true;
    } else if (headerScrolled && scrollY <= 24) {
      headerScrolled = false;
    }
    if (header) {
      header.dataset.scrolled = String(headerScrolled);
    }
  }

  // Math & Interpolation Utilities
  const clamp = x => Math.max(0, Math.min(1, x));
  const ease = (a, b, x) => {
    const t = clamp((x - a) / (b - a));
    return t * t * (3 - 2 * t);
  };
  const windowed = (x, a, b, c, d) => ease(a, b, x) * (1 - ease(c, d, x));

  // Multi-keyframe interpolation
  function interpolate(x, points) {
    for (let i = 1; i < points.length; i++) {
      if (x <= points[i][0]) {
        const t = ease(points[i - 1][0], points[i][0], x);
        return points[i - 1][1] + (points[i][1] - points[i - 1][1]) * t;
      }
    }
    return points[points.length - 1][1];
  }

  let frame = 0;
  let displayedProgress = null;

  function draw() {
    frame = 0;
    updateHeader();

    if (reduced.matches) return;

    const range = Math.max(1, journey.offsetHeight - innerHeight);
    const targetProgress = clamp(-journey.getBoundingClientRect().top / range);
    if (displayedProgress === null) displayedProgress = targetProgress;
    displayedProgress += (targetProgress - displayedProgress) * 0.16;
    if (Math.abs(targetProgress - displayedProgress) < 0.0005) {
      displayedProgress = targetProgress;
    } else {
      frame = requestAnimationFrame(draw);
    }
    const p = displayedProgress;
    if (header) header.dataset.darkScene = String(p > 0.2 && (root.dataset.theme !== 'light' || !followup || followup.getBoundingClientRect().top > 72));
    if (stage) stage.dataset.darkScene = String(p > 0.2);

    // Global scroll progress
    root.style.setProperty('--p', p.toFixed(4));

    // ----------------------------------------------------
    // 1. Room, House & Depth Camera
    // ----------------------------------------------------
    const enter = ease(0.06, 0.22, p);
    root.style.setProperty('--enter', enter.toFixed(4));
    root.style.setProperty('--portal-opacity', ease(0.08, 0.16, p).toFixed(3));
    root.style.setProperty('--portal-top', `${(15 * (1 - enter)).toFixed(2)}%`);
    root.style.setProperty('--portal-side', `${(33 * (1 - enter)).toFixed(2)}%`);
    root.style.setProperty('--portal-bottom', `${(13 * (1 - enter)).toFixed(2)}%`);

    const houseScale = interpolate(p, [
      [0, 1.0],
      [0.22, 1.18],
      [0.54, 1.36],
      [0.75, 1.52],
      [0.92, 1.68],
      [1.0, 1.78]
    ]);
    root.style.setProperty('--house-scale', houseScale.toFixed(3));

    const roomScale = interpolate(p, [
      [0, 1.0],
      [0.22, 1.2],
      [0.54, 1.38],
      [0.75, 1.54],
      [0.92, 1.7],
      [1.0, 1.8]
    ]);
    root.style.setProperty('--room-scale', roomScale.toFixed(3));

    root.style.setProperty('--house-x', '0%');
    root.style.setProperty('--house-opacity', (0.15 + 0.85 * ease(0.06, 0.22, p)).toFixed(3));
    root.style.setProperty('--wash-opacity', (1 - ease(0.07, 0.24, p)).toFixed(3));

    // ----------------------------------------------------
    // 2. Hero Typography
    // ----------------------------------------------------
    root.style.setProperty('--hero-opacity', (1 - ease(0.04, 0.13, p)).toFixed(3));

    // ----------------------------------------------------
    // 3. PERSISTENT SAME HEROINE CHOREOGRAPHY
    // Remains continuous throughout all transitions
    // ----------------------------------------------------
    root.style.setProperty('--char-x', '0vw');

    const charScale = interpolate(p, [
      [0, 1.0],
      [0.24, 1.05],
      [0.44, 1.2],
      [0.54, 1.05],
      [0.66, 1.1],
      [0.78, 1.06],
      [0.88, 1.0],
      [1.0, 0.98]
    ]);
    root.style.setProperty('--char-scale', charScale.toFixed(3));

    const charY = interpolate(p, [
      [0, 0],
      [0.24, 10],
      [0.44, 25],
      [0.54, 15],
      [0.66, 0],
      [0.78, 10],
      [1.0, 0]
    ]);
    root.style.setProperty('--char-y', `${charY.toFixed(1)}px`);

    // Poses of the EXACT same heroine
    const poseAthleteOp = 1;
    root.style.setProperty('--pose-athlete-op', poseAthleteOp.toFixed(3));
    // Keep the hero illustration out of the plan, coach and nutrition scenes.
    const characterSceneOp = 1 - ease(0.16, 0.20, p) + ease(0.92, 0.94, p);
    root.style.setProperty('--character-scene-op', clamp(characterSceneOp).toFixed(3));

    // Phone pose completely removed per request
    root.style.setProperty('--pose-phone-op', '0');

    root.style.setProperty('--pose-workout-op', '0');
    root.style.setProperty('--pose-food-op', '0');

    const charFilter = interpolate(p, [
      [0, 1.0],
      [0.22, 0.5],
      [0.52, 0.3],
      [0.82, 0.2],
      [1.0, 0.6]
    ]);
    root.style.setProperty('--char-filter', charFilter.toFixed(3));

    // ----------------------------------------------------
    // 4. SCENE 03: INPUT ENTERS PHONE -> GENERATION -> RESULT INSIDE
    // ----------------------------------------------------
    const planOp = windowed(p, 0.19, 0.23, 0.52, 0.54);
    root.style.setProperty('--plan-opacity', planOp.toFixed(3));

    // Input Stream (Left): streams straight towards phone
    // The heading and explanation stay with the phone until the whole chapter ends.
    // Individual cards still disappear as they travel into the device.
    const inputOp = windowed(p, 0.21, 0.25, 0.52, 0.54);
    root.style.setProperty('--input-stream-op', inputOp.toFixed(3));
    root.style.setProperty('--input-stream-x', '0px');
    // Each context card travels into the phone before the result becomes visible.
    [
      [0.27, 0.32],
      [0.29, 0.34],
      [0.31, 0.36]
    ].forEach(([start, end], index) => {
      const flight = ease(start, end, p);
      root.style.setProperty(`--card-flight-${index + 1}`, flight.toFixed(3));
      root.style.setProperty(`--phone-chip-${index + 1}`, ease(end - 0.01, end + 0.015, p).toFixed(3));
    });

    // Compact Phone: Center
    const deviceScale = 0.94 + 0.08 * ease(0.22, 0.34, p);
    root.style.setProperty('--device-scale', deviceScale.toFixed(3));
    root.style.setProperty('--device-x', '0px');

    // Processing Mesh vs Result Plan INSIDE the same phone screen
    const aiComputeOp = 1 - ease(0.395, 0.425, p);
    const planScreenOp = ease(0.395, 0.425, p);
    root.style.setProperty('--ai-compute-op', aiComputeOp.toFixed(3));
    root.style.setProperty('--plan-screen-op', planScreenOp.toFixed(3));
    if (aiStatusText) aiStatusText.textContent = p >= 0.405 ? 'Plan Ready' : 'AI Generating';

    // ----------------------------------------------------
    // 5. SCENE 04 & 05: DUAL FRAME COACH SYNC & DUAL FRAME KITCHEN
    // ----------------------------------------------------
    // Synchronized Split: Coach Demonstrates & Heroine Mirrors (Left & Right)
    const coachOp = windowed(p, 0.54, 0.56, 0.73, 0.75);
    root.style.setProperty('--coach-opacity', coachOp.toFixed(3));
    root.style.setProperty('--coach-scale', (0.96 + coachOp * 0.04).toFixed(3));

    // Dual Kitchen Split: Recipe Video & Heroine Cooking (Left & Right)
    const nutritionOp = windowed(p, 0.75, 0.77, 0.90, 0.92);
    root.style.setProperty('--nutrition-opacity', nutritionOp.toFixed(3));
    root.style.setProperty('--nutrition-scale', (0.96 + nutritionOp * 0.04).toFixed(3));

    // Finale
    const finaleOp = ease(0.92, 0.94, p);
    root.style.setProperty('--finale-opacity', finaleOp.toFixed(3));
    root.style.setProperty('--finale-scale', (0.96 + finaleOp * 0.04).toFixed(3));

    // Scene count indicator (Now 5 unified cohesive scenes)
    let currentScene = 1;
    if (p >= 0.20) currentScene = 2;
    if (p >= 0.54) currentScene = 3;
    if (p >= 0.75) currentScene = 4;
    if (p >= 0.92) currentScene = 5;
    count.textContent = `${String(currentScene).padStart(2, '0')} / 05`;
  }

  function update() {
    if (!frame) {
      frame = requestAnimationFrame(draw);
    }
  }

  window.addEventListener('scroll', update, { passive: true });
  window.addEventListener('resize', update, { passive: true });
  reduced.addEventListener?.('change', update);
  document.addEventListener('lift:theme-change', update);

  // Ensure videos reliably play when scrolled into view
  const coachVid = document.querySelector('.coach-live-video');
  const foodVid = document.querySelector('.food-live-video');

  function ensureVideosPlay() {
    if (coachVid && coachVid.paused) {
      coachVid.play().catch(() => {});
    }
    if (foodVid && foodVid.paused) {
      foodVid.play().catch(() => {});
    }
  }

  window.addEventListener('scroll', ensureVideosPlay, { passive: true, once: false });
  document.addEventListener('click', ensureVideosPlay, { once: true });
  document.addEventListener('touchstart', ensureVideosPlay, { once: true });

  draw();
})();
