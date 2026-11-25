"use strict";
(() => {
  const plans = {
    free: { title: "Your Free journey", summary: "The essentials for building a routine and seeing what you complete.", steps: [
      ["Choose a starting point", "Decide whether you want to move more, eat more consistently, or simply build a routine.", "goal.png"],
      ["Log your activity", "Record workouts and meals in one place as you go through your week.", "routine.png"],
      ["Look back at your habits", "Use your activity history to notice what helps you stay consistent.", "progress.png"],
      ["Keep your momentum", "Set your next focus based on what you have completed and what fits your life now.", "ai-plan.png"]
    ]},
    coach: { title: "Your AI + Coach journey", summary: "Personalized planning, then human support for the training side.", steps: [
      ["Set your goal", "Tell LIFT what you want to achieve and where you are starting.", "coach-1.png"],
      ["Share your real routine", "Add your available days and time, training space, fitness level, and relevant health considerations.", "coach-2.png"],
      ["Get an AI starting plan", "LIFT shapes workout and nutrition guidance around the details you shared.", "coach-3.png"],
      ["Choose a coach", "Select an available coach for training guidance and support.", "coach-4.png"],
      ["Review and practice", "Your coach can review the training plan, answer questions, and guide the way you perform exercises.", "coach-5.png"],
      ["Track and adjust", "Record what you complete and revisit the training plan with your coach as your routine changes.", "progress.png"]
    ]},
    nutrition: { title: "Your AI + Nutritionist journey", summary: "Personalized planning, then human support for the food side.", steps: [
      ["Set your goal", "Tell LIFT what you want to achieve and what your current routine looks like.", "nutrition-1.png"],
      ["Share your food context", "Add your schedule, preferences, food habits, and relevant health considerations.", "nutrition-2.png"],
      ["Get an AI starting plan", "LIFT shapes workout and nutrition guidance around your context.", "nutrition-3.png"],
      ["Choose a nutritionist", "Select an available nutritionist for guidance on the food side of your plan.", "nutrition-4.png"],
      ["Make it practical", "Review the food plan with your nutritionist and discuss meals that fit your time and preferences.", "nutrition-5.png"],
      ["Log and refine", "Record meals and activity, then use your progress to discuss useful food adjustments.", "progress.png"]
    ]},
    premium: { title: "Your Premium journey", summary: "AI planning with a coach for training and a nutritionist for food.", steps: [
      ["Define your starting point", "Share your goal and what you would like to change in your routine.", "premium-1.png"],
      ["Bring your day into focus", "Add your schedule, training space, food preferences, and relevant health considerations.", "premium-2.png"],
      ["Get AI plan drafts", "LIFT uses your details to prepare a training and nutrition starting point.", "premium-3.png"],
      ["Choose your specialists", "Select a coach for training and a nutritionist for food.", "premium-4.png"],
      ["Shape your training", "Your coach reviews the training side and helps you put the exercises into practice.", "coach-5.png"],
      ["Shape your nutrition", "Your nutritionist reviews the food side and helps make meals work for your routine.", "nutrition-5.png"],
      ["Follow progress together", "Track what you do, message each specialist in their area, and revisit plans when your needs change.", "premium-5.png"]
    ]}
  };
  const grid = document.getElementById("steps-grid");
  if (!grid) return;
  const cards = [...document.querySelectorAll(".plan-card")];
  function render(key) {
    const plan = plans[key];
    if (!plan) return;
    document.getElementById("sequence-title").textContent = plan.title;
    document.getElementById("sequence-summary").textContent = `${plan.steps.length} connected phases. ${plan.summary}`;
    grid.replaceChildren(...plan.steps.map(([title, description, image], index) => {
      const article = document.createElement("article"); article.className = "step-card";
      const media = document.createElement("div"); media.className = "step-media";
      const img = document.createElement("img"); img.src = `/Public/HowItWorks/assets/${image}`; img.alt = ""; img.loading = index < 2 ? "eager" : "lazy";
      const number = document.createElement("span"); number.className = "step-number"; number.textContent = `PHASE ${String(index + 1).padStart(2, "0")}`;
      media.append(img, number);
      const body = document.createElement("div"); body.className = "step-body";
      const heading = document.createElement("h3"); heading.textContent = title;
      const text = document.createElement("p"); text.textContent = description;
      body.append(heading, text); article.append(media, body); return article;
    }));
    cards.forEach(card => { const active = card.dataset.plan === key; card.classList.toggle("is-active", active); card.setAttribute("aria-pressed", String(active)); card.querySelector(".plan-action").firstChild.textContent = active ? "Selected journey " : "View journey "; });
  }
  cards.forEach(card => card.addEventListener("click", () => render(card.dataset.plan)));
  const requested = new URLSearchParams(location.search).get("plan");
  render(Object.hasOwn(plans, requested) ? requested : "coach");
})();
