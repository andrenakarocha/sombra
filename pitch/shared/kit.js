// Sombra · pitch — utilitários de animação compartilhados pelos atos (copiados do pitch HENRY).
// Tudo aqui é determinístico e amarrado ao timeline do ato (nada de tween solto).
(function () {
  const T = window.TIMING;
  const K = { T };
  const $ = (sel, root) => (typeof sel === "string" ? (root || document).querySelector(sel) : sel);
  const $$ = (sel, root) => (typeof sel === "string" ? Array.from((root || document).querySelectorAll(sel)) : [].concat(sel));
  K.$ = $; K.$$ = $$;

  const norm = (w) => w.toLowerCase().normalize("NFD").replace(/[̀-ͯ]/g, "").replace(/[^a-z0-9]/g, "");

  // ---------- ato ----------
  K.act = function (actId) {
    const act = T.acts[actId];
    const tl = gsap.timeline({ paused: true });
    K.tl = tl; K.actId = actId; K.actDur = act.dur;
    const vars = window.__hyperframes && window.__hyperframes.getVariables ? window.__hyperframes.getVariables() : {};
    if (vars.legendas === false) document.documentElement.classList.add("no-captions");
    return tl;
  };

  // Contexto de cena: tempos absolutos dentro do ato e âncoras na narração.
  K.scene = function (id) {
    const s = T.scenes[id];
    const words = s.texto.trim().split(/\s+/).map(norm);
    const vo0 = s.t0 + s.vo0;
    return {
      id, el: document.getElementById(id), t0: s.t0, dur: s.dur, end: s.t0 + s.dur, vo0, voDur: s.voDur,
      at: (sec) => s.t0 + sec,
      p: (frac) => vo0 + frac * s.voDur,
      // instante em que a n-ésima palavra iniciada por `token` é dita (alinhado ao áudio pelo sync)
      w: (token, nth) => {
        const key = norm(token); let seen = 0;
        for (let i = 0; i < words.length; i++) {
          if (words[i].startsWith(key) && ++seen === (nth || 1)) return vo0 + (s.palavras ? s.palavras[i] : (i / words.length) * s.voDur);
        }
        console.warn("palavra não encontrada no roteiro:", id, token);
        return vo0;
      },
    };
  };

  // ---------- entradas ----------
  const IN = {
    up:    [{ opacity: 0, y: 54 }, { opacity: 1, y: 0, duration: 0.62, ease: "power3.out" }],
    down:  [{ opacity: 0, y: -44 }, { opacity: 1, y: 0, duration: 0.5, ease: "power2.out" }],
    left:  [{ opacity: 0, x: -80 }, { opacity: 1, x: 0, duration: 0.6, ease: "expo.out" }],
    right: [{ opacity: 0, x: 80 }, { opacity: 1, x: 0, duration: 0.6, ease: "expo.out" }],
    scale: [{ opacity: 0, scale: 0.84 }, { opacity: 1, scale: 1, duration: 0.55, ease: "back.out(1.3)" }],
    pop:   [{ opacity: 0, scale: 0.6 }, { opacity: 1, scale: 1, duration: 0.4, ease: "back.out(1.7)" }],
    fade:  [{ opacity: 0 }, { opacity: 1, duration: 0.7, ease: "sine.out" }],
    slam:  [{ opacity: 0, scale: 1.45 }, { opacity: 1, scale: 1, duration: 0.34, ease: "power4.out" }],
    wipe:  [{ clipPath: "inset(0% 100% 0% 0%)" }, { clipPath: "inset(0% 0% 0% 0%)", duration: 0.75, ease: "power3.inOut" }],
    wipeUp:[{ clipPath: "inset(100% 0% 0% 0%)" }, { clipPath: "inset(0% 0% 0% 0%)", duration: 0.7, ease: "power3.out" }],
    grow:  [{ scaleX: 0 }, { scaleX: 1, duration: 0.75, ease: "power3.out" }],
    growY: [{ scaleY: 0 }, { scaleY: 1, duration: 0.7, ease: "power3.out" }],
    tilt:  [{ opacity: 0, rotationY: -28, x: 90, transformPerspective: 1400 }, { opacity: 1, rotationY: 0, x: 0, transformPerspective: 1400, duration: 0.8, ease: "power3.out" }],
  };
  // K.in(seletor, tempo, tipo, {stagger, duration, ease, ...})
  K.in = function (sel, t, kind, o) {
    const [from, to] = IN[kind || "up"];
    const els = $$(sel);
    if (!els.length) { console.warn("K.in sem alvo:", sel); return; }
    K.tl.fromTo(els, Object.assign({}, from), Object.assign({}, to, o || {}), t);
  };

  K.count = function (sel, from, to, t, dur, fmt, ease) {
    const el = $(sel); const o = { v: from };
    const f = fmt || ((v) => String(Math.round(v)));
    el.textContent = f(from);
    K.tl.to(o, { v: to, duration: dur, ease: ease || "power2.out", onUpdate: () => { el.textContent = f(o.v); } }, t);
  };

  K.draw = function (sel, t, dur, ease) {
    $$(sel).forEach((p) => {
      const len = p.getTotalLength();
      p.style.strokeDasharray = len;
      K.tl.fromTo(p, { strokeDashoffset: len }, { strokeDashoffset: 0, duration: dur, ease: ease || "power2.inOut" }, t);
    });
  };

  // ---------- chat ----------
  // Bolhas já estão no DOM na posição final; `times` dá o instante de cada uma.
  K.chat = function (root, times) {
    const tl = K.tl;
    $$(".msg", $(root)).forEach((m, i) => {
      const t = times[i]; if (t == null) return;
      const bubble = $(".bubble", m);
      if (m.classList.contains("henry")) {
        const dots = $(".typing", m);
        if (dots) {
          tl.fromTo(dots, { opacity: 0, scale: 0.8 }, { opacity: 1, scale: 1, duration: 0.2, ease: "power2.out" }, t - 0.75);
          tl.fromTo($$("i", dots), { y: 0 }, { y: -7, duration: 0.18, ease: "sine.inOut", yoyo: true, repeat: 3, stagger: 0.09 }, t - 0.68);
          tl.set(dots, { opacity: 0 }, t);
        }
        tl.fromTo(bubble, { opacity: 0, y: 16, scale: 0.95, transformOrigin: "0% 0%" }, { opacity: 1, y: 0, scale: 1, duration: 0.38, ease: "back.out(1.4)" }, t);
      } else {
        tl.fromTo(bubble, { opacity: 0, x: 36, scale: 0.9, transformOrigin: "100% 50%" }, { opacity: 1, x: 0, scale: 1, duration: 0.32, ease: "power3.out" }, t);
      }
    });
  };

  // ---------- terminal ----------
  // Revela as linhas uma a uma e rola quando passa de `visible`.
  K.term = function (root, t, o) {
    const tl = K.tl; const opt = Object.assign({ per: 0.14, visible: 10 }, o || {});
    const body = $(root); const wrap = $(".term-lines", body); const lines = $$(".term-line", wrap);
    lines.forEach((ln, i) => tl.fromTo(ln, { opacity: 0 }, { opacity: 1, duration: 0.01 }, t + i * opt.per));
    const extra = lines.length - opt.visible;
    if (extra > 0) {
      const h = lines[1].offsetTop - lines[0].offsetTop;
      tl.fromTo(wrap, { y: 0 }, { y: -extra * h, duration: extra * opt.per, ease: "none" }, t + opt.visible * opt.per);
    }
    return t + lines.length * opt.per;
  };

  // ---------- transições entre cenas ----------
  function wipeLayer() {
    let w = document.getElementById("k-wipe");
    if (w) return w;
    w = document.createElement("div"); w.id = "k-wipe";
    w.style.cssText = "position:absolute;inset:0;z-index:20;overflow:hidden;pointer-events:none";
    ["#D9A441", "#B04F27", "#0B3B39"].forEach((c) => {
      const b = document.createElement("div");
      b.style.cssText = "position:absolute;top:-80px;left:-380px;width:2680px;height:1240px;transform:translateX(-2900px) skewX(-14deg);background:" + c;
      w.appendChild(b);
    });
    document.getElementById("root").appendChild(w);
    return w;
  }
  K.cut = function (fromId, toId, T0, kind) {
    const tl = K.tl; const a = document.getElementById(fromId); const b = document.getElementById(toId);
    switch (kind || "push") {
      case "push":
        tl.to(a, { x: -1920, duration: 0.62, ease: "power3.inOut" }, T0);
        tl.fromTo(b, { x: 1920, opacity: 1 }, { x: 0, opacity: 1, duration: 0.62, ease: "power3.inOut" }, T0);
        tl.set(a, { opacity: 0 }, T0 + 0.62);
        return T0 + 0.62;
      case "pushUp":
        tl.to(a, { y: -1080, duration: 0.62, ease: "power3.inOut" }, T0);
        tl.fromTo(b, { y: 1080, opacity: 1 }, { y: 0, opacity: 1, duration: 0.62, ease: "power3.inOut" }, T0);
        tl.set(a, { opacity: 0 }, T0 + 0.62);
        return T0 + 0.62;
      case "blur":
        tl.to(a, { filter: "blur(16px)", scale: 1.05, opacity: 0, duration: 0.42, ease: "power2.in" }, T0);
        tl.fromTo(b, { filter: "blur(16px)", scale: 0.95, opacity: 0 }, { filter: "blur(0px)", scale: 1, opacity: 1, duration: 0.46, ease: "power2.out" }, T0 + 0.22);
        return T0 + 0.68;
      case "zoom":
        tl.to(a, { scale: 2.4, opacity: 0, filter: "blur(8px)", duration: 0.42, ease: "power3.in" }, T0);
        tl.fromTo(b, { scale: 0.55, opacity: 0, filter: "blur(8px)" }, { scale: 1, opacity: 1, filter: "blur(0px)", duration: 0.5, ease: "power3.out" }, T0 + 0.16);
        return T0 + 0.66;
      case "wipe": {
        const blocks = Array.from(wipeLayer().children);
        tl.fromTo(blocks, { x: -2900 }, { x: 0, duration: 0.42, ease: "power3.in", stagger: 0.07, immediateRender: false }, T0);
        tl.set(a, { opacity: 0 }, T0 + 0.56);
        tl.set(b, { opacity: 1 }, T0 + 0.56);
        tl.fromTo(blocks, { x: 0 }, { x: 2900, duration: 0.46, ease: "power3.out", stagger: 0.07, immediateRender: false }, T0 + 0.6);
        return T0 + 1.2;
      }
    }
  };

  // ---------- HUD, legendas, véu, fundo ----------
  K.chrome = function () {
    const tl = K.tl; const act = T.acts[K.actId];
    // rótulo de seção
    const holder = $(".hud-section");
    let prev = null; let prevEl = null;
    act.scenes.forEach((id) => {
      const s = T.scenes[id];
      if (s.sec === prev) return;
      const span = document.createElement("span"); span.textContent = s.sec; holder.appendChild(span);
      if (prevEl) tl.to(prevEl, { opacity: 0, x: 24, duration: 0.25, ease: "power2.in" }, s.t0 - 0.1);
      tl.fromTo(span, { opacity: 0, x: -24 }, { opacity: 1, x: 0, duration: 0.45, ease: "power3.out" }, s.t0 + 0.35);
      prev = s.sec; prevEl = span;
    });
    // progresso do vídeo inteiro
    const bar = $(".hud-progress i");
    tl.fromTo(bar, { scaleX: act.offset / T.total }, { scaleX: (act.offset + act.dur) / T.total, duration: act.dur, ease: "none" }, 0);
    const track = $(".hud-progress");
    Object.values(T.acts).forEach((a) => {
      if (a.offset <= 0) return;
      const tick = document.createElement("b"); tick.style.left = (a.offset / T.total) * 100 + "%"; track.appendChild(tick);
    });
    // véu de abertura e fechamento do ato
    tl.fromTo("#veil", { opacity: 1 }, { opacity: 0, duration: 0.5, ease: "power2.out" }, 0);
    tl.fromTo("#veil", { opacity: 0 }, { opacity: 1, duration: 0.42, ease: "power2.in", immediateRender: false }, act.dur - 0.42);
    // fundo vivo
    tl.fromTo(".bg-leaves", { x: 0, y: 0, rotation: 0 }, { x: -140, y: 60, rotation: 1.5, duration: act.dur, ease: "none" }, 0);
    tl.fromTo(".bg-glow.a", { x: 0, y: 0, scale: 1 }, { x: 160, y: 90, scale: 1.12, duration: act.dur / 2, ease: "sine.inOut", yoyo: true, repeat: 1 }, 0);
    tl.fromTo(".bg-glow.b", { x: 0, y: 0 }, { x: -140, y: -70, duration: act.dur / 2, ease: "sine.inOut", yoyo: true, repeat: 1 }, 0);
  };

  function chunk(text) {
    const parts = text.match(/[^.?!:;,]+[.?!:;,]*/g).map((p) => p.trim()).filter(Boolean);
    const out = []; let cur = "";
    const n = (s) => (s ? s.split(/\s+/).length : 0);
    parts.forEach((p) => {
      if (cur && n(cur) + n(p) > 13) { out.push(cur); cur = p; }
      else cur = cur ? cur + " " + p : p;
      if (n(cur) >= 8 && /[.?!]$/.test(cur)) { out.push(cur); cur = ""; }
    });
    if (cur) { if (out.length && n(cur) < 4 && n(out[out.length - 1]) + n(cur) <= 15) out[out.length - 1] += " " + cur; else out.push(cur); }
    return out;
  }
  K.captions = function () {
    const tl = K.tl; const act = T.acts[K.actId]; const holder = $("#caps");
    act.scenes.forEach((id) => {
      const s = T.scenes[id]; const chunks = chunk(s.texto);
      const total = s.texto.trim().split(/\s+/).length; let acc = 0;
      chunks.forEach((c, i) => {
        const nw = c.split(/\s+/).length;
        const at = (n) => s.t0 + s.vo0 + (s.palavras ? (n < total ? s.palavras[n] : s.voDur) : (n / total) * s.voDur);
        const start = at(acc); acc += nw;
        const end = i === chunks.length - 1 ? s.t0 + s.vo0 + s.voDur + 0.35 : at(acc);
        const el = document.createElement("div"); el.className = "cap"; el.textContent = c; holder.appendChild(el);
        tl.set(el, { opacity: 1 }, Math.max(0.01, start));
        tl.set(el, { opacity: 0 }, end);
      });
    });
  };

  K.finish = function () {
    window.__timelines = window.__timelines || {};
    window.__timelines[K.actId] = K.tl;
  };

  window.K = K;
})();
