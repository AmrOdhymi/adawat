/* Adawat — design preview interactions. Static showcase, no app code. */
(function () {
  "use strict";

  var DESIGNS = [
    {
      id: "fluent",
      name: "Fluent Calm",
      tagline: "A calm, native Windows surface. Layered neutrals, one confident blue, and motion you barely notice.",
      traits: ["Native Windows 10/11", "Light + dark", "Comfortable density", "Low motion"],
      meta: [["Best for", "Everyday desktop"], ["Corner radius", "8–10 px"], ["Density", "Comfortable"], ["Motion", "180 ms ease-out"]],
      color: "Neutral layers with a single blue accent.",
      typeNote: "Segoe UI Variable · system native",
      typeDisplay: "Segoe UI Variable Display",
      typeBody: "Segoe UI Variable Text",
      shape: "8–10 px corners · soft 1 px lines",
      motion: "180 ms ease-out · tiny lift on press",
      defaultMode: "light",
      sw: ["#0f6cbd", "#d9e9f9"]
    },
    {
      id: "paper",
      name: "Paper & Ink",
      tagline: "Warm paper, dark ink, and serif headings. A writing-first direction for people who keep Adawat open all day.",
      traits: ["Warm paper tones", "Serif display", "Flat borders", "Minimal motion"],
      meta: [["Best for", "Long writing sessions"], ["Corner radius", "2–4 px"], ["Density", "Relaxed"], ["Motion", "140 ms linear"]],
      color: "Warm paper, ink text, rust accent.",
      typeNote: "Georgia display · Segoe UI body",
      typeDisplay: "Georgia",
      typeBody: "Segoe UI",
      shape: "2–4 px corners · hard 1 px rules",
      motion: "140 ms · almost no movement",
      defaultMode: "light",
      sw: ["#9a3b2c", "#e9dcc3"]
    },
    {
      id: "mint",
      name: "Soft Mint",
      tagline: "Rounded, bright, and friendly. Mint surfaces keep utilities feeling light without turning playful.",
      traits: ["Large radii", "Mint accent", "Soft shadows", "Pill controls"],
      meta: [["Best for", "Casual everyday use"], ["Corner radius", "14–24 px"], ["Density", "Airy"], ["Motion", "220 ms ease-out"]],
      color: "Mint accent on near-white green.",
      typeNote: "Segoe UI Variable · rounded rhythm",
      typeDisplay: "Segoe UI Variable Display",
      typeBody: "Segoe UI Variable Text",
      shape: "14–24 px corners · pill controls",
      motion: "220 ms · soft scale on press",
      defaultMode: "light",
      sw: ["#0b7d5c", "#cdeede"]
    },
    {
      id: "graphite",
      name: "Graphite Pro",
      tagline: "A dense, technical workspace. Graphite surfaces, amber signal, and mono labels for people who scan fast.",
      traits: ["Dark by default", "Mono micro-labels", "Dense layout", "Rounded corners"],
      meta: [["Best for", "Power users"], ["Corner radius", "6–20 px"], ["Density", "Compact"], ["Motion", "120 ms linear"]],
      color: "Graphite surfaces with amber signal.",
      typeNote: "Segoe UI Variable · Cascadia Mono labels",
      typeDisplay: "Segoe UI Variable Display",
      typeBody: "Segoe UI Variable Text",
      shape: "6–20 px corners · 1 px technical rules",
      motion: "120 ms · instant feedback",
      defaultMode: "dark",
      sw: ["#e2a33f", "#1d2227"]
    },
    {
      id: "violet",
      name: "Violet Studio",
      tagline: "Editorial spacing with a violet signal. Designed for creative work that needs focus and a little atmosphere.",
      traits: ["Dark by default", "Violet accent", "Editorial spacing", "Soft depth"],
      meta: [["Best for", "Creative workflows"], ["Corner radius", "12–18 px"], ["Density", "Spacious"], ["Motion", "200 ms ease-out"]],
      color: "Deep violet surfaces, lavender signal.",
      typeNote: "Segoe UI Variable Display · editorial scale",
      typeDisplay: "Segoe UI Variable Display",
      typeBody: "Segoe UI Variable Text",
      shape: "12–18 px corners · hairline borders",
      motion: "200 ms · gentle fade",
      defaultMode: "dark",
      sw: ["#a78bfa", "#241e34"]
    }
  ];

  var TOOLS_MAIN = [
    { label: "Convert", icon: "i-convert" },
    { label: "Compress", icon: "i-compress" },
    { label: "Image", icon: "i-image" },
    { label: "PDF", icon: "i-file" },
    { label: "QR", icon: "i-qr" },
    { label: "Folder", icon: "i-folder" }
  ];

  var TOOLS_QUICK = [
    { label: "Screenshot", icon: "i-camera" },
    { label: "Make QR", icon: "i-qr" },
    { label: "Scan QR", icon: "i-scan" },
    { label: "Fix Layout", icon: "i-layout" },
    { label: "Launcher", icon: "i-grid" },
    { label: "Notepad", icon: "i-note" },
    { label: "Clipboard", icon: "i-clipboard" },
    { label: "Extract Text", icon: "i-text" }
  ];

  var KEY = "adawat.design-preview.v1";
  var root = document.documentElement;
  var state = { design: "fluent", modes: {} };

  try {
    var saved = JSON.parse(localStorage.getItem(KEY) || "{}");
    if (saved && typeof saved === "object") {
      if (saved.modes && typeof saved.modes === "object") state.modes = saved.modes;
      if (saved.design && DESIGNS.some(function (d) { return d.id === saved.design; })) state.design = saved.design;
    }
  } catch (err) { /* storage may be blocked on file://; the preview still works */ }

  function persist() {
    try { localStorage.setItem(KEY, JSON.stringify(state)); } catch (err) { /* ignore */ }
  }

  function designById(id) {
    for (var i = 0; i < DESIGNS.length; i++) if (DESIGNS[i].id === id) return DESIGNS[i];
    return DESIGNS[0];
  }

  /* ---------------- Top bar ---------------- */

  var switcher = document.getElementById("switcher");
  var modeButtons = Array.prototype.slice.call(document.querySelectorAll("[data-mode-btn]"));

  DESIGNS.forEach(function (d, i) {
    var btn = document.createElement("button");
    btn.type = "button";
    btn.className = "switch-btn";
    btn.dataset.design = d.id;
    btn.setAttribute("aria-pressed", "false");
    btn.style.setProperty("--sw1", d.sw[0]);
    btn.style.setProperty("--sw2", d.sw[1]);
    btn.innerHTML = '<span class="switch-btn__num">0' + (i + 1) + '</span><span class="switch-btn__swatch" aria-hidden="true"></span><span>' + d.name + "</span>";
    btn.addEventListener("click", function () { applyDesign(d.id); });
    switcher.appendChild(btn);
  });

  modeButtons.forEach(function (btn) {
    btn.addEventListener("click", function () {
      var next = btn.dataset.modeBtn;
      state.modes[state.design] = next;
      applyDesign(state.design, next);
    });
  });

  function applyDesign(id, forcedMode) {
    var d = designById(id);
    state.design = d.id;
    var mode = forcedMode || state.modes[d.id] || d.defaultMode;
    if (mode !== "light" && mode !== "dark") mode = d.defaultMode;
    state.modes[d.id] = mode;

    root.dataset.design = d.id;
    root.dataset.mode = mode;

    var idx = DESIGNS.indexOf(d);
    document.getElementById("introIndex").textContent = "0" + (idx + 1) + " / 05";
    document.getElementById("designName").textContent = d.name;
    document.getElementById("designTagline").textContent = d.tagline;
    document.title = d.name + " — Adawat design directions";

    var traits = document.getElementById("designTraits");
    traits.innerHTML = "";
    d.traits.forEach(function (t) {
      var li = document.createElement("li");
      li.textContent = t;
      traits.appendChild(li);
    });

    var meta = document.getElementById("designMeta");
    meta.innerHTML = "";
    d.meta.forEach(function (pair) {
      var wrap = document.createElement("div");
      var dt = document.createElement("dt");
      var dd = document.createElement("dd");
      dt.textContent = pair[0];
      dd.textContent = pair[1];
      wrap.appendChild(dt);
      wrap.appendChild(dd);
      meta.appendChild(wrap);
    });

    document.getElementById("langColor").textContent = d.color;
    document.getElementById("langTypeNote").textContent = d.typeNote;
    document.getElementById("langShape").textContent = d.shape;
    document.getElementById("langMotion").textContent = d.motion;
    document.getElementById("typeDisplay").textContent = d.typeDisplay;
    document.getElementById("typeBody").textContent = d.typeBody;

    Array.prototype.forEach.call(switcher.children, function (btn) {
      var on = btn.dataset.design === d.id;
      btn.classList.toggle("is-on", on);
      btn.setAttribute("aria-pressed", on ? "true" : "false");
    });

    modeButtons.forEach(function (btn) {
      var on = btn.dataset.modeBtn === mode;
      btn.classList.toggle("is-on", on);
      btn.setAttribute("aria-pressed", on ? "true" : "false");
    });

    updateHexTokens();
    persist();
  }

  function updateHexTokens() {
    var cs = getComputedStyle(root);
    Array.prototype.forEach.call(document.querySelectorAll("[data-token]"), function (el) {
      var value = cs.getPropertyValue(el.dataset.token).trim();
      el.textContent = value ? value.toUpperCase() : "—";
    });
  }

  /* ---------------- Wheel ---------------- */

  var NS = "http://www.w3.org/2000/svg";

  function polar(cx, cy, r, deg) {
    var a = (deg - 90) * Math.PI / 180;
    return [cx + r * Math.cos(a), cy + r * Math.sin(a)];
  }

  function wedgePath(cx, cy, ri, ro, a0, a1) {
    var o0 = polar(cx, cy, ro, a0);
    var o1 = polar(cx, cy, ro, a1);
    var i1 = polar(cx, cy, ri, a1);
    var i0 = polar(cx, cy, ri, a0);
    var large = (a1 - a0) > 180 ? 1 : 0;
    return "M" + o0[0].toFixed(2) + " " + o0[1].toFixed(2) +
      "A" + ro + " " + ro + " 0 " + large + " 1 " + o1[0].toFixed(2) + " " + o1[1].toFixed(2) +
      "L" + i1[0].toFixed(2) + " " + i1[1].toFixed(2) +
      "A" + ri + " " + ri + " 0 " + large + " 0 " + i0[0].toFixed(2) + " " + i0[1].toFixed(2) + "Z";
  }

  function buildWheel(el, tools, quick) {
    var size = parseFloat(el.dataset.size) || 356;
    var cx = size / 2;
    var ro = size * 0.486;
    var ri = size * 0.171;
    el.style.setProperty("--wheel-size", size + "px");
    el.classList.toggle("wheel--quick", !!quick);

    var svg = document.createElementNS(NS, "svg");
    svg.setAttribute("viewBox", "0 0 " + size + " " + size);

    var step = 360 / tools.length;
    var labels = [];

    tools.forEach(function (tool, i) {
      var path = document.createElementNS(NS, "path");
      path.setAttribute("d", wedgePath(cx, cx, ri, ro, i * step + 1.5, (i + 1) * step - 1.5));
      path.setAttribute("class", "wheel__petal");
      path.dataset.tool = tool.label;
      path.dataset.index = String(i);
      path.setAttribute("role", "button");
      path.setAttribute("tabindex", "0");
      path.setAttribute("aria-label", tool.label);
      svg.appendChild(path);
    });

    var ring = document.createElementNS(NS, "circle");
    ring.setAttribute("cx", cx);
    ring.setAttribute("cy", cx);
    ring.setAttribute("r", ri);
    ring.setAttribute("class", "wheel__hub-ring");
    svg.appendChild(ring);

    el.appendChild(svg);

    tools.forEach(function (tool, i) {
      var mid = (i + 0.5) * step;
      var radius = (ri + ro) / 2;
      var pt = polar(cx, cx, radius, mid);
      var label = document.createElement("div");
      label.className = "wheel__label";
      label.style.left = (pt[0] / size * 100) + "%";
      label.style.top = (pt[1] / size * 100) + "%";
      label.innerHTML = '<svg class="ico"><use href="#' + tool.icon + '"></use></svg><span>' + tool.label + "</span>";
      el.appendChild(label);
      labels.push(label);
    });

    var hub = document.createElement("div");
    hub.className = "wheel__hub";
    hub.innerHTML = quick
      ? '<svg class="ico"><use href="#i-bolt"></use></svg>'
      : '<span class="brand__mark" aria-hidden="true"><i></i><i></i><i></i><i></i></span><small>Adawat</small>';
    el.appendChild(hub);

    svg.addEventListener("click", function (e) {
      var path = e.target.closest ? e.target.closest(".wheel__petal") : null;
      if (!path) return;
      toast(path.dataset.tool + " · selected");
    });

    svg.addEventListener("mouseover", function (e) {
      var path = e.target.closest ? e.target.closest(".wheel__petal") : null;
      if (!path) return;
      var label = labels[Number(path.dataset.index)];
      if (label) label.classList.add("is-hot");
    });

    svg.addEventListener("mouseout", function (e) {
      var path = e.target.closest ? e.target.closest(".wheel__petal") : null;
      if (!path) return;
      var label = labels[Number(path.dataset.index)];
      if (label) label.classList.remove("is-hot");
    });

    svg.addEventListener("keydown", function (e) {
      if (e.key !== "Enter" && e.key !== " ") return;
      var path = e.target.closest ? e.target.closest(".wheel__petal") : null;
      if (!path) return;
      e.preventDefault();
      toast(path.dataset.tool + " · selected");
    });
  }

  Array.prototype.forEach.call(document.querySelectorAll("[data-wheel]"), function (el) {
    buildWheel(el, el.dataset.wheel === "quick" ? TOOLS_QUICK : TOOLS_MAIN, el.dataset.wheel === "quick");
  });

  /* ---------------- HUD waveform ---------------- */

  Array.prototype.forEach.call(document.querySelectorAll(".hud__wave"), function (wave) {
    for (var i = 0; i < 26; i++) {
      var bar = document.createElement("i");
      var h = 5 + Math.abs(Math.sin(i * 0.9) * 17) + (i % 3) * 2;
      bar.style.height = Math.min(24, Math.round(h)) + "px";
      bar.style.opacity = String(0.45 + (i % 5) * 0.1);
      wave.appendChild(bar);
    }
  });

  /* ---------------- QR sample ---------------- */

  (function buildQr() {
    var grid = document.getElementById("qrGrid");
    if (!grid) return;
    var n = 25;
    var seed = 20261117;
    function rnd() {
      seed = (seed * 1103515245 + 12345) & 0x7fffffff;
      return seed / 0x7fffffff;
    }
    function inFinder(x, y) {
      return (x < 7 && y < 7) || (x > n - 8 && y < 7) || (x < 7 && y > n - 8);
    }
    function finderOn(x, y) {
      var lx = x > n - 8 ? x - (n - 7) : x;
      var ly = y > n - 8 ? y - (n - 7) : y;
      var edge = lx === 0 || lx === 6 || ly === 0 || ly === 6;
      var core = lx >= 2 && lx <= 4 && ly >= 2 && ly <= 4;
      return edge || core;
    }
    for (var y = 0; y < n; y++) {
      for (var x = 0; x < n; x++) {
        var cell = document.createElement("i");
        var on;
        if (inFinder(x, y)) on = finderOn(x, y);
        else if (x === 6 || y === 6) on = (x + y) % 2 === 0;
        else on = rnd() > 0.52;
        if (on) cell.className = "on";
        grid.appendChild(cell);
      }
    }
  })();

  /* ---------------- Toast ---------------- */

  var toastEl = document.getElementById("toast");
  var toastTimer = 0;

  function toast(message) {
    toastEl.textContent = message;
    toastEl.classList.add("is-on");
    window.clearTimeout(toastTimer);
    toastTimer = window.setTimeout(function () {
      toastEl.classList.remove("is-on");
    }, 1700);
  }

  document.addEventListener("click", function (e) {
    var target = e.target.closest ? e.target.closest("[data-toast]") : null;
    if (!target) return;
    toast(target.dataset.toast);
  });

  /* ---------------- Clipboard filter ---------------- */

  var search = document.getElementById("clipSearch");
  var clipItems = Array.prototype.slice.call(document.querySelectorAll("#clipList .clip-item"));
  var clipEmpty = document.getElementById("clipEmpty");

  function clipKind(item) {
    var meta = item.querySelector("small");
    return meta ? meta.textContent.split("·")[0].trim().toLowerCase() : "";
  }

  function filterClipboard() {
    var q = (search && search.value || "").trim().toLowerCase();
    var active = document.querySelector(".chip-row[data-segmented] .chip.is-on");
    var kind = active ? active.textContent.trim().toLowerCase() : "all";
    var singular = kind.replace(/s$/, "");
    var shown = 0;
    clipItems.forEach(function (item) {
      var text = (item.dataset.text || "").toLowerCase();
      var itemKind = clipKind(item);
      var kindOk = kind === "all" || itemKind === singular || itemKind.indexOf(singular) === 0;
      var textOk = !q || text.indexOf(q) !== -1;
      var on = kindOk && textOk;
      item.hidden = !on;
      if (on) shown++;
    });
    if (clipEmpty) clipEmpty.hidden = shown !== 0;
  }

  if (search) search.addEventListener("input", filterClipboard);

  /* ---------------- Segmented controls + tabs + toggles ---------------- */

  document.addEventListener("click", function (e) {
    var segBtn = e.target.closest ? e.target.closest("[data-segmented] button") : null;
    if (segBtn) {
      var group = segBtn.parentElement;
      Array.prototype.forEach.call(group.children, function (btn) {
        btn.classList.toggle("is-on", btn === segBtn);
      });
      if (group.classList.contains("chip-row")) filterClipboard();
      return;
    }

    var tab = e.target.closest ? e.target.closest(".settings__tab") : null;
    if (tab) {
      Array.prototype.forEach.call(tab.parentElement.children, function (btn) {
        btn.classList.toggle("is-on", btn === tab);
      });
      return;
    }

    var toggle = e.target.closest ? e.target.closest(".toggle") : null;
    if (toggle) {
      var on = !toggle.classList.contains("is-on");
      toggle.classList.toggle("is-on", on);
      toggle.setAttribute("aria-checked", on ? "true" : "false");
    }
  });

  /* ---------------- Keyboard ---------------- */

  document.addEventListener("keydown", function (e) {
    if (e.ctrlKey || e.metaKey || e.altKey) return;
    var t = e.target;
    if (t && (t.tagName === "INPUT" || t.tagName === "TEXTAREA" || t.isContentEditable)) return;

    if (e.key >= "1" && e.key <= "5") {
      var d = DESIGNS[Number(e.key) - 1];
      if (d) applyDesign(d.id);
      return;
    }
    if (e.key === "l" || e.key === "L") {
      var next = root.dataset.mode === "dark" ? "light" : "dark";
      state.modes[state.design] = next;
      applyDesign(state.design, next);
    }
  });

  /* ---------------- Boot ---------------- */

  applyDesign(state.design);
  filterClipboard();
})();
