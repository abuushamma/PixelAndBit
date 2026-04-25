(() => {
  const onReady = () => {
    const reduceMotion = window.matchMedia?.("(prefers-reduced-motion: reduce)")?.matches;

    // Fade-in on enter
    const fadeEls = Array.from(document.querySelectorAll(".pb-fade"));
    const animEls = Array.from(document.querySelectorAll(".pb-anim"));
    if ("IntersectionObserver" in window) {
      const io = new IntersectionObserver(
        (entries) => {
          for (const entry of entries) {
            if (entry.isIntersecting) {
              const el = entry.target;
              const delayStep = Math.max(0, Math.min(12, Number(el.getAttribute("data-pb-stagger") || "0")));
              if (!reduceMotion && delayStep) {
                el.style.transitionDelay = `${delayStep * 70}ms`;
              }
              el.classList.add("pb-in");
              io.unobserve(entry.target);
            }
          }
        },
        { root: null, rootMargin: "0px 0px -10% 0px", threshold: 0.08 }
      );
      fadeEls.forEach((el) => io.observe(el));
      animEls.forEach((el) => io.observe(el));
    } else {
      fadeEls.forEach((el) => el.classList.add("pb-in"));
      animEls.forEach((el) => el.classList.add("pb-in"));
    }

    // Subtle parallax-like background drift (CSS reads --pb-scroll)
    const root = document.documentElement;
    let ticking = false;
    const onScroll = () => {
      if (ticking) return;
      ticking = true;
      window.requestAnimationFrame(() => {
        root.style.setProperty("--pb-scroll", `${window.scrollY}px`);
        ticking = false;
      });
    };
    window.addEventListener("scroll", onScroll, { passive: true });
    onScroll();

    // Navbar: add "scrolled" state for stronger glass
    const nav = document.querySelector(".pb-navbar");
    if (nav) {
      let navTick = false;
      const onNavScroll = () => {
        if (navTick) return;
        navTick = true;
        window.requestAnimationFrame(() => {
          nav.classList.toggle("pb-navbar--scrolled", window.scrollY > 8);
          navTick = false;
        });
      };
      window.addEventListener("scroll", onNavScroll, { passive: true });
      onNavScroll();
    }

    // Mobile offcanvas: close on navigation click
    const mobileNav = document.getElementById("pbMobileNav");
    if (mobileNav && window.bootstrap?.Offcanvas) {
      mobileNav.addEventListener("click", (e) => {
        const a = e.target?.closest?.("a[data-bs-dismiss='offcanvas']");
        if (!a) return;
        const inst = window.bootstrap.Offcanvas.getInstance(mobileNav) || new window.bootstrap.Offcanvas(mobileNav);
        inst.hide();
      });
    }

    // Cart: add-to-cart (AJAX) + badge pop (synced across mobile + desktop)
    const badges = Array.from(document.querySelectorAll(".js-pb-cart-badge"));
    const countEls = Array.from(document.querySelectorAll(".js-pb-cart-count"));

    const popBadges = () => {
      for (const b of badges) {
        b.classList.remove("pb-cart-pop");
        void b.offsetWidth;
        b.classList.add("pb-cart-pop");
      }
    };

    const setCount = (next) => {
      if (!countEls.length) return;
      const prev = Number(countEls[0].textContent || "0");
      for (const el of countEls) el.textContent = String(next);
      for (const b of badges) b.classList.toggle("d-none", next === 0);
      if (next !== prev) popBadges();
    };

    // Back to top (footer)
    const backTop = document.getElementById("pb-back-to-top");
    if (backTop) {
      backTop.addEventListener("click", () => {
        const top = document.getElementById("page-top");
        (top || document.documentElement).scrollIntoView({ behavior: reduceMotion ? "auto" : "smooth" });
        backTop.blur();
      });
    }

    // Account dropdown: show on hover for fine pointers (desktop); tap/click still works
    if (window.bootstrap?.Dropdown && window.matchMedia("(pointer: fine) and (min-width: 768px)").matches) {
      for (const dd of document.querySelectorAll(".pb-nav-user-dd")) {
        const btn = dd.querySelector('[data-bs-toggle="dropdown"]');
        if (!btn) continue;
        const inst = window.bootstrap.Dropdown.getOrCreateInstance
          ? window.bootstrap.Dropdown.getOrCreateInstance(btn, { autoClose: true })
          : new window.bootstrap.Dropdown(btn, { autoClose: true });
        let hideT;
        dd.addEventListener("mouseenter", () => {
          clearTimeout(hideT);
          inst.show();
        });
        dd.addEventListener("mouseleave", () => {
          hideT = window.setTimeout(() => inst.hide(), 140);
        });
      }
    }

    document.addEventListener("click", async (e) => {
      const btn = e.target?.closest?.("[data-add-to-cart]");
      if (!btn) return;

      const productId = Number(btn.getAttribute("data-product-id") || "0");
      if (!productId) return;

      btn.disabled = true;
      const original = btn.textContent;
      btn.textContent = "Adding…";

      try {
        const csrf = window.__pb?.csrf;
        const res = await fetch("/Cart/Add", {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
            ...(csrf ? { RequestVerificationToken: csrf } : {}),
          },
          body: JSON.stringify({ productId, quantity: 1 }),
        });

        if (!res.ok) throw new Error("add failed");
        const data = await res.json();
        if (typeof data.count === "number") setCount(data.count);

        btn.textContent = "Added";
        setTimeout(() => {
          btn.textContent = original;
        }, 650);
      } catch {
        btn.textContent = "Retry";
        setTimeout(() => {
          btn.textContent = original;
        }, 900);
      } finally {
        btn.disabled = false;
      }
    });

    // Parallax (mouse) for hero floating elements
    const parallaxEls = Array.from(document.querySelectorAll("[data-parallax]"));
    if (!reduceMotion && parallaxEls.length) {
      let raf = 0;
      let mx = 0;
      let my = 0;

      const onMove = (e) => {
        const vw = window.innerWidth || 1;
        const vh = window.innerHeight || 1;
        mx = (e.clientX / vw - 0.5) * 2;
        my = (e.clientY / vh - 0.5) * 2;
        if (raf) return;
        raf = window.requestAnimationFrame(() => {
          for (const el of parallaxEls) {
            const strength = Number(el.getAttribute("data-parallax-strength") || "12");
            const rx = (-my * strength) * 0.4;
            const ry = (mx * strength) * 0.6;
            const tx = mx * strength;
            const ty = my * strength;
            el.style.transform = `translate3d(${tx}px, ${ty}px, 0) rotateX(${rx}deg) rotateY(${ry}deg)`;
          }
          raf = 0;
        });
      };

      window.addEventListener("mousemove", onMove, { passive: true });
    }

    // Horizontal "Our work" gentle auto-drift (desktop only)
    const hz = document.querySelector("[data-pb-hz]");
    if (!reduceMotion && hz && window.matchMedia("(pointer:fine)").matches) {
      let last = 0;
      let dir = 1;
      const speed = 0.18; // px per ms
      const tick = (t) => {
        if (!last) last = t;
        const dt = t - last;
        last = t;

        const maxScroll = hz.scrollWidth - hz.clientWidth;
        if (maxScroll > 10) {
          hz.scrollLeft += dir * dt * speed;
          if (hz.scrollLeft >= maxScroll - 2) dir = -1;
          if (hz.scrollLeft <= 2) dir = 1;
        }
        window.requestAnimationFrame(tick);
      };
      window.requestAnimationFrame(tick);
    }
  };

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", onReady);
  } else {
    onReady();
  }
})();
