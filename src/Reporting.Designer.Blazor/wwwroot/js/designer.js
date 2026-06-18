// OmniReport · Designer host-side helpers.
// Loaded by hosts via <script src="_content/Reporting.Designer.Blazor/js/designer.js"></script>.
// All state is kept inside this module — Blazor only sees commits (one per gesture).

(function () {
    "use strict";

    const mm = 3.43; // px per mm (matches --mm in tokens.css with --page-w=720)
    const state = {
        active: null,        // active gesture descriptor
        dotnetRef: null,     // .NET reference for callback
    };

    function px2mm(px) { return px / mm; }

    // ───── Zoom awareness ──────────────────────────────────────────────────────
    // The .canvas-stage carries `transform: scale(z)` so every CSS pixel of the page
    // renders as z viewport pixels. Mouse events report deltas in VIEWPORT units, but
    // element.style.left/top are CSS units. Without dividing by z, drag/resize/marquee
    // gestures move twice as fast at 200% zoom, half as fast at 50%, etc.
    function getCanvasZoom() {
        const stage = document.querySelector(".canvas-stage");
        if (!stage) return 1;
        // Inline style first (cheap); fall back to computed matrix.
        const inline = stage.style.transform;
        if (inline) {
            const m = inline.match(/scale\(\s*([\d.]+)\s*\)/);
            if (m) return parseFloat(m[1]) || 1;
        }
        const computed = getComputedStyle(stage).transform;
        if (computed && computed !== "none") {
            const m = computed.match(/matrix\(\s*([^,]+),/);
            if (m) return parseFloat(m[1]) || 1;
        }
        return 1;
    }

    // ───── Zoom: reservar a caixa ESCALADA para o container de scroll ─────────────
    // O .canvas-stage recebe `transform: scale(z)` (origin 0 0). `transform` é PAINT:
    // amplia o visual mas NÃO cresce a caixa de layout — então o .canvas-scroll
    // (overflow:auto) não enxerga a página ampliada e não mostra scrollbar em zoom > 1
    // (direita/baixo ficam inalcançáveis). Mantemos o transform (getCanvasZoom() e as
    // réguas continuam iguais) e reservamos o excedente (z-1)× como MARGEM — que é
    // layout puro e NÃO é afetada pelo transform do próprio elemento. Assim a área de
    // scroll do pai = natural + natural·(z-1) = natural·z = o tamanho visual escalado.
    // Medimos o .page-shell (offset* são px de layout, imunes ao transform) para evitar
    // loop de feedback com o ResizeObserver do .canvas-scroll.
    function sizeZoomBox() {
        const stage = document.querySelector(".canvas-stage");
        if (!stage) return;
        const shell = stage.querySelector(".page-shell");
        if (!shell) { stage.style.marginRight = ""; stage.style.marginBottom = ""; return; }
        const z = getCanvasZoom();
        if (z <= 1) { stage.style.marginRight = "0px"; stage.style.marginBottom = "0px"; return; }
        const cs = getComputedStyle(stage);
        const padX = (parseFloat(cs.paddingLeft) || 0) + (parseFloat(cs.paddingRight)  || 0);
        const padY = (parseFloat(cs.paddingTop)  || 0) + (parseFloat(cs.paddingBottom) || 0);
        const naturalW = shell.offsetWidth  + padX;   // px de layout (não escalados)
        const naturalH = shell.offsetHeight + padY;
        stage.style.marginRight  = `${Math.ceil(naturalW * (z - 1))}px`;
        stage.style.marginBottom = `${Math.ceil(naturalH * (z - 1))}px`;
    }

    // CSS-pixel offset of `el` relative to `ancestor`, walking the offsetParent chain.
    // offsetLeft/Top are zoom-independent because they're declared dimensions, not
    // rendered ones — perfect for converting between page-relative coords and band-
    // relative coords without re-scaling.
    function offsetWithin(el, ancestor) {
        let x = 0, y = 0, n = el;
        while (n && n !== ancestor) {
            x += n.offsetLeft;
            y += n.offsetTop;
            n = n.offsetParent;
        }
        return { x, y };
    }

    function getElementMetrics(target) {
        const x = parseFloat(target.style.left) || 0;
        const y = parseFloat(target.style.top) || 0;
        const w = parseFloat(target.style.width) || 0;
        const h = parseFloat(target.style.height) || 0;
        return { x, y, w, h };
    }

    function snap(value, gridPx, enabled) {
        if (!enabled || gridPx <= 0) return value;
        return Math.round(value / gridPx) * gridPx;
    }

    // ───── 1. Element move ──────────────────────────────────────────────────────
    function startElementMove(ev, element, options) {
        ev.preventDefault();
        const m = getElementMetrics(element);
        // If the user pressed-down on an element that is part of an existing multi-selection,
        // the whole selection moves together (Visual Studio / DevExpress / Figma). Build a
        // snapshot of every selected element's starting metrics so the entire group can be
        // shifted by the same delta on every pointermove. If the press is on an element NOT
        // in the current selection, fall back to single-element move — picking up the new one
        // de-facto replaces the selection on commit (Blazor will reconcile via OnGestureCommit).
        const others = [];
        if (element.classList.contains("is-selected")) {
            document.querySelectorAll(".el.is-selected[data-element-id]").forEach(el => {
                if (el === element) return;
                const om = getElementMetrics(el);
                others.push({ el, origX: om.x, origY: om.y, elementId: el.dataset.elementId, bandKind: el.dataset.bandKind });
            });
        }
        state.active = {
            kind: "move",
            element,
            startX: ev.clientX,
            startY: ev.clientY,
            origX: m.x,
            origY: m.y,
            gridPx: (options.snapMm || 1) * mm,
            snap: !!options.snap,
            elementId: element.dataset.elementId,
            bandKind: element.dataset.bandKind,
            others,
        };
        document.addEventListener("pointermove", onPointerMove);
        document.addEventListener("pointerup",   onPointerUp,   { once: true });
        element.setPointerCapture?.(ev.pointerId);
    }

    function startElementResize(ev, element, handle, options) {
        ev.preventDefault();
        const m = getElementMetrics(element);
        state.active = {
            kind: "resize",
            handle,
            element,
            startX: ev.clientX,
            startY: ev.clientY,
            origX: m.x, origY: m.y, origW: m.w, origH: m.h,
            gridPx: (options.snapMm || 1) * mm,
            snap: !!options.snap,
            elementId: element.dataset.elementId,
            bandKind: element.dataset.bandKind,
        };
        document.addEventListener("pointermove", onPointerMove);
        document.addEventListener("pointerup",   onPointerUp,   { once: true });
    }

    // Reads the parent band's inner dimensions (the .band-content box) in CSS pixels.
    // clientWidth/Height are zoom-independent — using getBoundingClientRect here would
    // return scaled viewport pixels and the confinement clamp would over-grow at zoom > 1.
    function getBandLimits(element) {
        const bandContent = element.closest?.(".band-content");
        if (!bandContent) return null;
        return { width: bandContent.clientWidth, height: bandContent.clientHeight };
    }

    function onPointerMove(ev) {
        const a = state.active;
        if (!a) return;
        // Convert viewport-pixel delta to CSS-pixel delta. At zoom 200% a 20 px mouse
        // move = 10 CSS pixels of element travel — without dividing here the element
        // would race ahead of the cursor.
        const z = getCanvasZoom();
        const dx = (ev.clientX - a.startX) / z;
        const dy = (ev.clientY - a.startY) / z;

        if (a.kind === "move") {
            let nx = snap(a.origX + dx, a.gridPx, a.snap);
            let ny = snap(a.origY + dy, a.gridPx, a.snap);
            // Smart-guide snap to other elements within threshold
            const guides = computeSmartGuides(a.element, nx, ny);
            if (guides.snapX !== null) nx = guides.snapX;
            if (guides.snapY !== null) ny = guides.snapY;
            // Confine the element to its band — Telerik / SSRS behavior. To move an
            // element to another band the user must Cut + click target band + Paste.
            const w = parseFloat(a.element.style.width)  || 0;
            const h = parseFloat(a.element.style.height) || 0;
            const limits = getBandLimits(a.element);
            if (limits) {
                nx = Math.max(0, Math.min(nx, Math.max(0, limits.width  - w)));
                ny = Math.max(0, Math.min(ny, Math.max(0, limits.height - h)));
            } else {
                nx = Math.max(0, nx);
                ny = Math.max(0, ny);
            }
            // Effective delta after clamping (guides + band confinement) — use this delta
            // for siblings so the whole group moves uniformly even when the leader bumps
            // against a band edge. Siblings still get individually confined to their own
            // bands (cross-band groups are rare but supported).
            const realDx = nx - a.origX;
            const realDy = ny - a.origY;
            a.element.style.left = `${nx}px`;
            a.element.style.top  = `${ny}px`;
            if (a.others && a.others.length) {
                for (const o of a.others) {
                    const ow = parseFloat(o.el.style.width)  || 0;
                    const oh = parseFloat(o.el.style.height) || 0;
                    let ox = o.origX + realDx;
                    let oy = o.origY + realDy;
                    const olim = getBandLimits(o.el);
                    if (olim) {
                        ox = Math.max(0, Math.min(ox, Math.max(0, olim.width  - ow)));
                        oy = Math.max(0, Math.min(oy, Math.max(0, olim.height - oh)));
                    } else {
                        ox = Math.max(0, ox);
                        oy = Math.max(0, oy);
                    }
                    o.el.style.left = `${ox}px`;
                    o.el.style.top  = `${oy}px`;
                }
            }
        } else if (a.kind === "resize") {
            applyResize(a, dx, dy);
        }
    }

    // Visual Studio / WinForms-style smart guides. Lines extend ONLY between the dragged
    // element and the reference it's aligning to — never across the whole page — so the
    // user can immediately see which elements are aligning.
    //
    // Snaps to:
    //   • edges of other elements (left/right/top/bottom)
    //   • centers of other elements (centerX / centerY)
    //   • band-content edges (left=0 / right=bandWidth / top=0 / bottom=bandHeight)
    //   • band centers (centerX of band, centerY of band)
    //
    // All math is done in PAGE-relative coordinates so cross-band alignment works
    // correctly (e.g. a Detail TextBox can align with a PageHeader column).
    function computeSmartGuides(target, nx, ny) {
        const pageEl = document.querySelector(".page");
        if (!pageEl) return { snapX: null, snapY: null };
        clearGuides();

        const bandContent = target.closest(".band-content");
        if (!bandContent) return { snapX: null, snapY: null };
        // All math in CSS pixels (zoom-independent). offsetWithin walks offsetParent
        // and sums offsetLeft/Top — those are declared dimensions, NOT scaled by
        // transform. clientWidth/Height are similarly CSS-unit measurements.
        const bandOffset = offsetWithin(bandContent, pageEl);
        const bandLeftInPage = bandOffset.x;
        const bandTopInPage  = bandOffset.y;
        const bandW = bandContent.clientWidth;
        const bandH = bandContent.clientHeight;

        const w = parseFloat(target.style.width)  || 0;
        const h = parseFloat(target.style.height) || 0;
        // Target rectangle in PAGE coordinates at the candidate future position
        const tL = bandLeftInPage + nx,  tR = tL + w,  tCX = tL + w / 2;
        const tT = bandTopInPage  + ny,  tB = tT + h,  tCY = tT + h / 2;

        const threshold = 5;     // px snap radius
        let snapX = null, snapY = null;

        // Each match accumulates the Y-range (for vertical guides) or X-range (for
        // horizontal guides) so the rendered line spans from the highest involved edge
        // to the lowest — never the whole page.
        const vMatches = new Map(); // pageX → { yTop, yBottom }
        const hMatches = new Map(); // pageY → { xLeft, xRight }

        // Snap precedence:
        //   1. Center matches (priority = 1) — user intent is usually "center this"
        //   2. Edge matches (priority = 0)
        // When multiple candidates fall within the threshold, the highest-priority match
        // is chosen so a center-snap isn't shadowed by an edge-snap on tiny gaps (e.g. a
        // band only 2 mm taller than the element, where centering and aligning-to-edge
        // both trigger at once).
        let snapXPriority = -1, snapYPriority = -1;
        function recordV(pageX, refTop, refBottom, snapNx, priority = 0) {
            if (priority > snapXPriority) { snapX = snapNx; snapXPriority = priority; }
            else if (snapX === null) { snapX = snapNx; snapXPriority = priority; }
            const key = Math.round(pageX * 2) / 2; // 0.5-px bucket avoids dupes from float
            const top = Math.min(tT, refTop);
            const bot = Math.max(tB, refBottom);
            const cur = vMatches.get(key);
            vMatches.set(key, cur ? { yTop: Math.min(cur.yTop, top), yBottom: Math.max(cur.yBottom, bot) }
                                  : { yTop: top, yBottom: bot });
        }
        function recordH(pageY, refLeft, refRight, snapNy, priority = 0) {
            if (priority > snapYPriority) { snapY = snapNy; snapYPriority = priority; }
            else if (snapY === null) { snapY = snapNy; snapYPriority = priority; }
            const key = Math.round(pageY * 2) / 2;
            const left  = Math.min(tL, refLeft);
            const right = Math.max(tR, refRight);
            const cur = hMatches.get(key);
            hMatches.set(key, cur ? { xLeft: Math.min(cur.xLeft, left), xRight: Math.max(cur.xRight, right) }
                                  : { xLeft: left, xRight: right });
        }

        // ── 1. Band-content edges + center (always available, cheap) ──────────────
        const bL = bandLeftInPage, bR = bandLeftInPage + bandW, bCX = bandLeftInPage + bandW / 2;
        const bT = bandTopInPage,  bB = bandTopInPage  + bandH, bCY = bandTopInPage  + bandH / 2;
        const bandEdgesX = [
            { ref: tCX, ot: bCX, snapNx: (bandW - w) / 2, prio: 1 }, // center first → priority
            { ref: tL,  ot: bL,  snapNx: 0,               prio: 0 },
            { ref: tR,  ot: bR,  snapNx: bandW - w,       prio: 0 },
        ];
        const bandEdgesY = [
            { ref: tCY, ot: bCY, snapNy: (bandH - h) / 2, prio: 1 },
            { ref: tT,  ot: bT,  snapNy: 0,               prio: 0 },
            { ref: tB,  ot: bB,  snapNy: bandH - h,       prio: 0 },
        ];
        for (const c of bandEdgesX) {
            if (Math.abs(c.ref - c.ot) < threshold) {
                recordV(c.ot, bT, bB, c.snapNx, c.prio);
            }
        }
        for (const c of bandEdgesY) {
            if (Math.abs(c.ref - c.ot) < threshold) {
                recordH(c.ot, bL, bR, c.snapNy, c.prio);
            }
        }

        // ── 2. Sibling elements (all bands — cross-band alignment supported) ──────
        pageEl.querySelectorAll(".el[data-element-id]").forEach(other => {
            if (other === target) return;
            // CSS-pixel coords relative to .page. offsetWithin already returns the
            // element's offsetLeft/Top sum — that's the top-left corner. clientWidth
            // and clientHeight give the dimensions, also in CSS pixels.
            const off = offsetWithin(other, pageEl);
            const oL = off.x;
            const oT = off.y;
            const oR = oL + other.clientWidth;
            const oB = oT + other.clientHeight;
            const oCX = (oL + oR) / 2;
            const oCY = (oT + oB) / 2;

            // X-axis candidates: align (target edge) to (other edge or center).
            // prio=1 for center-to-center so it shadows edge snaps when both apply.
            const xCases = [
                { ref: tCX, ot: oCX, pageX: oCX, snapNx: nx + (oCX - tCX), prio: 1 },
                { ref: tL,  ot: oL,  pageX: oL,  snapNx: nx + (oL  - tL),  prio: 0 },
                { ref: tR,  ot: oR,  pageX: oR,  snapNx: nx + (oR  - tR),  prio: 0 },
                { ref: tL,  ot: oR,  pageX: oR,  snapNx: nx + (oR  - tL),  prio: 0 },
                { ref: tR,  ot: oL,  pageX: oL,  snapNx: nx + (oL  - tR),  prio: 0 },
            ];
            for (const c of xCases) {
                if (Math.abs(c.ref - c.ot) < threshold) recordV(c.pageX, oT, oB, c.snapNx, c.prio);
            }
            const yCases = [
                { ref: tCY, ot: oCY, pageY: oCY, snapNy: ny + (oCY - tCY), prio: 1 },
                { ref: tT,  ot: oT,  pageY: oT,  snapNy: ny + (oT  - tT),  prio: 0 },
                { ref: tB,  ot: oB,  pageY: oB,  snapNy: ny + (oB  - tB),  prio: 0 },
                { ref: tT,  ot: oB,  pageY: oB,  snapNy: ny + (oB  - tT),  prio: 0 },
                { ref: tB,  ot: oT,  pageY: oT,  snapNy: ny + (oT  - tB),  prio: 0 },
            ];
            for (const c of yCases) {
                if (Math.abs(c.ref - c.ot) < threshold) recordH(c.pageY, oL, oR, c.snapNy, c.prio);
            }
        });

        // ── 2b. User guides (dragged out from the rulers) — elements snap to them too.
        //       Guide coordinates are page-space CSS px = mm × (720/210), the same space as
        //       tL/tT, so we can compare directly. priority 1 so guides win over edge snaps.
        const userGuides = (rulerState && rulerState.guides) ? rulerState.guides : null;
        if (userGuides) {
            for (const gu of userGuides) {
                const gpx = gu.mm * RULER_BASE_MM;
                if (gu.axis === "v") {
                    const cx = [
                        { ref: tCX, snapNx: nx + (gpx - tCX) },
                        { ref: tL,  snapNx: nx + (gpx - tL) },
                        { ref: tR,  snapNx: nx + (gpx - tR) },
                    ];
                    for (const c of cx) if (Math.abs(c.ref - gpx) < threshold) recordV(gpx, tT, tB, c.snapNx, 1);
                } else {
                    const cy = [
                        { ref: tCY, snapNy: ny + (gpx - tCY) },
                        { ref: tT,  snapNy: ny + (gpx - tT) },
                        { ref: tB,  snapNy: ny + (gpx - tB) },
                    ];
                    for (const c of cy) if (Math.abs(c.ref - gpx) < threshold) recordH(gpx, tL, tR, c.snapNy, 1);
                }
            }
        }

        // ── 3. Render the consolidated guides — short lines spanning only the
        //       involved elements, with a 6 px overshoot for visual clarity.
        const overshoot = 6;
        for (const [pageX, { yTop, yBottom }] of vMatches) {
            renderVGuide(pageEl, pageX, yTop - overshoot, yBottom - yTop + overshoot * 2);
        }
        for (const [pageY, { xLeft, xRight }] of hMatches) {
            renderHGuide(pageEl, pageY, xLeft - overshoot, xRight - xLeft + overshoot * 2);
        }

        return { snapX, snapY };
    }

    let guideEls = [];
    function clearGuides() {
        guideEls.forEach(g => g.remove());
        guideEls = [];
    }
    function renderVGuide(pageEl, pageX, top, height) {
        const g = document.createElement("div");
        g.className = "smart-guide v";
        // Sub-pixel positioning (centered on the half-pixel) keeps the 1-px line crisp.
        g.style.cssText =
            `position:absolute;left:${pageX - 0.5}px;top:${top}px;` +
            `width:1px;height:${height}px;` +
            `background:var(--smart-guide,#ff2d92);z-index:25;pointer-events:none;`;
        pageEl.appendChild(g);
        guideEls.push(g);
    }
    function renderHGuide(pageEl, pageY, left, width) {
        const g = document.createElement("div");
        g.className = "smart-guide h";
        g.style.cssText =
            `position:absolute;top:${pageY - 0.5}px;left:${left}px;` +
            `height:1px;width:${width}px;` +
            `background:var(--smart-guide,#ff2d92);z-index:25;pointer-events:none;`;
        pageEl.appendChild(g);
        guideEls.push(g);
    }

    function applyResize(a, dx, dy) {
        let x = a.origX, y = a.origY, w = a.origW, h = a.origH;
        const minW = 2 * mm, minH = 2 * mm;
        if (a.handle.includes("e")) w = Math.max(minW, a.origW + dx);
        if (a.handle.includes("s")) h = Math.max(minH, a.origH + dy);
        if (a.handle.includes("w")) {
            const newW = Math.max(minW, a.origW - dx);
            x = a.origX + (a.origW - newW);
            w = newW;
        }
        if (a.handle.includes("n")) {
            const newH = Math.max(minH, a.origH - dy);
            y = a.origY + (a.origH - newH);
            h = newH;
        }
        x = snap(x, a.gridPx, a.snap);
        y = snap(y, a.gridPx, a.snap);
        w = snap(w, a.gridPx, a.snap);
        h = snap(h, a.gridPx, a.snap);
        // Band confinement — Telerik style. Clamp the element so the bounding box stays
        // inside .band-content. If the user wants a taller element they must grow the
        // band first via its bottom-edge resize handle.
        const limits = getBandLimits(a.element);
        if (limits) {
            if (x < 0) { w = Math.max(minW, w + x); x = 0; }
            if (y < 0) { h = Math.max(minH, h + y); y = 0; }
            if (x + w > limits.width)  w = Math.max(minW, limits.width  - x);
            if (y + h > limits.height) h = Math.max(minH, limits.height - y);
        }
        a.element.style.left = `${x}px`;
        a.element.style.top  = `${y}px`;
        a.element.style.width  = `${w}px`;
        a.element.style.height = `${h}px`;
    }

    function onPointerUp(ev) {
        const a = state.active;
        if (!a) return;
        document.removeEventListener("pointermove", onPointerMove);
        const pageEl = document.querySelector(".page");
        clearGuides();
        const m = getElementMetrics(a.element);
        state.active = null;

        // If pointer never crossed the dead-zone the element's metrics are still its
        // origin values — treat this as a plain click (selection only) and skip the
        // round-trip to the server. Sending a no-op gesture otherwise triggers spurious
        // server-side logic (e.g. auto-grow re-evaluation) on every click.
        const unchanged = m.x === a.origX && m.y === a.origY
                       && (a.kind !== "resize" || (m.w === a.origW && m.h === a.origH));
        if (unchanged) return;

        const payload = {
            kind: a.kind,
            elementId: a.elementId,
            bandKind: a.bandKind,
            xMm: px2mm(m.x),
            yMm: px2mm(m.y),
            widthMm: px2mm(m.w),
            heightMm: px2mm(m.h),
        };
        if (state.dotnetRef) {
            state.dotnetRef.invokeMethodAsync("OnGestureCommit", payload)
                .catch(err => console.warn("[designer] gesture commit failed", err));
            // Commit sibling moves too (multi-drag) — each one as a separate gesture so the
            // server can do per-element clamp/auto-grow. They're all "move" kind regardless
            // of how the gesture started on the leader.
            if (a.kind === "move" && a.others && a.others.length) {
                for (const o of a.others) {
                    const om = getElementMetrics(o.el);
                    state.dotnetRef.invokeMethodAsync("OnGestureCommit", {
                        kind: "move",
                        elementId: o.elementId,
                        bandKind: o.bandKind,
                        xMm: px2mm(om.x),
                        yMm: px2mm(om.y),
                        widthMm: px2mm(om.w),
                        heightMm: px2mm(om.h),
                    }).catch(err => console.warn("[designer] sibling commit failed", err));
                }
            }
        }
    }

    // ───── 2. HTML5 drag/drop from toolbox to canvas ────────────────────────────
    // Per-item wiring (rarely needed — kept for API compatibility). The toolbox markup
    // already has draggable="true" and data-kind, so the delegated handler below catches
    // every dragstart without needing attachToolbox() calls from .NET.
    function setupToolboxDrag(item, kind) {
        item.setAttribute("draggable", "true");
        if (kind && !item.dataset.kind) item.dataset.kind = kind;
        item.addEventListener("dragstart", ev => {
            ev.dataTransfer.setData("application/x-omni-toolbox-kind", item.dataset.kind || kind);
            ev.dataTransfer.effectAllowed = "copy";
            item.classList.add("is-drag-source");
        });
        item.addEventListener("dragend", () => item.classList.remove("is-drag-source"));
    }

    // ───── Preview-mode keyboard scroll ──────────────────────────────────────────
    // When the user is inside the preview region (.preview-scroll is rendered), arrow
    // keys / PageUp / PageDown / Home / End scroll the paper rectangle — same behaviour
    // as Acrobat, Foxit, and the SSRS Report Viewer. The handler is gated so it doesn't
    // hijack the design-mode arrow-key move of selected elements.
    document.addEventListener("keydown", ev => {
        const preview = document.querySelector(".preview-scroll");
        if (!preview) return;                                  // not in preview mode
        if (ev.ctrlKey || ev.metaKey || ev.altKey) return;     // leave shortcuts alone
        // Don't fight inputs / editors that might be focused (export filename prompts, etc.)
        if (ev.target?.closest?.('input, textarea, select, [contenteditable="true"], .monaco-editor')) return;

        const lineStep = 60;                                   // px per arrow press
        const pageStep = preview.clientHeight - 40;            // overlap of 40 px like PDF viewers
        let dx = 0, dy = 0, absolute = null;

        switch (ev.key) {
            case "ArrowUp":    dy = -lineStep; break;
            case "ArrowDown":  dy = +lineStep; break;
            case "ArrowLeft":  dx = -lineStep; break;
            case "ArrowRight": dx = +lineStep; break;
            case "PageUp":     dy = -pageStep; break;
            case "PageDown":   dy = +pageStep; break;
            case " ":          dy = ev.shiftKey ? -pageStep : +pageStep; break; // Space / Shift+Space
            case "Home":       absolute = { top: 0,                     left: 0 }; break;
            case "End":        absolute = { top: preview.scrollHeight,  left: 0 }; break;
            default: return;
        }
        ev.preventDefault();
        if (absolute) {
            preview.scrollTo({ ...absolute, behavior: "smooth" });
        } else {
            preview.scrollBy({ left: dx, top: dy, behavior: "smooth" });
        }
    }, { capture: true });

    // ───── Ctrl+Wheel → zoom; bare wheel → normal scroll inside the canvas ────
    // The browser default for Ctrl+Wheel is "zoom the whole page" — which is wrong inside
    // an authoring tool. Capture it on the canvas-scroll host and route through OnZoomDelta
    // on the .NET side so the designer's State.Zoom advances. Bare wheel is left alone so
    // the canvas-scroll container handles native vertical scroll.
    document.addEventListener("wheel", ev => {
        if (!ev.ctrlKey && !ev.metaKey) return;
        const host = ev.target?.closest?.("[data-omni-zoom-host]");
        if (!host) return;
        ev.preventDefault();   // suppress browser zoom
        // deltaY > 0 = wheel down = zoom out. Step proportional to wheel speed.
        // Math.sign keeps it predictable across trackpads vs mouse wheels.
        const direction = ev.deltaY > 0 ? -1 : +1;
        const step = ev.shiftKey ? 0.25 : 0.10;
        if (state.dotnetRef) {
            state.dotnetRef.invokeMethodAsync("OnZoomDelta", direction * step)
                .catch(err => console.warn("[designer] zoom delta failed", err));
        }
    }, { passive: false });

    // ───── Browser-shortcut interceptor (Ctrl+A inside designer) ───────────────
    // Blazor's @onkeydown processes the event AFTER the browser's default. Ctrl+A on the
    // designer canvas would let the browser select every text node on the page. Suppress
    // it (and a couple of other VS-style shortcuts) at the capture phase whenever focus is
    // inside the .report-designer host but not inside a real editable surface (input /
    // textarea / contenteditable / Monaco editor).
    document.addEventListener("keydown", ev => {
        const inDesigner = ev.target?.closest?.(".report-designer");
        if (!inDesigner) return;
        const inEditable = ev.target?.closest?.(
            'input, textarea, [contenteditable="true"], .monaco-editor, .monaco-host'
        );
        if (inEditable) return;
        if ((ev.ctrlKey || ev.metaKey) && !ev.altKey) {
            const k = ev.key.toLowerCase();
            // Letters we own at the designer level. Browser would otherwise hijack 'a'
            // (Select All on the document), 'd' (bookmark), 's' (save page), 'o' (open).
            if (k === "a" || k === "d" || k === "s" || k === "o") {
                ev.preventDefault();
            }
        }
    }, { capture: true });

    // ───── Context menu (right-click on element / band / page) ─────────────────
    // Delegated handler. Determines what was clicked from the closest matching element
    // and dispatches to the .NET side via OnContextMenuRequest. The browser's own context
    // menu is suppressed only for our specific targets — other right-clicks (link, image
    // outside canvas, devtools needs) keep their native menus.
    document.addEventListener("contextmenu", ev => {
        if (!state.dotnetRef) return;
        const tgt = ev.target?.closest?.(".el[data-element-id], .band-content[data-band-kind], .band-strip, .page");
        if (!tgt) return;
        ev.preventDefault();
        let target, id = null;
        if (tgt.matches(".el[data-element-id]")) {
            target = "element";
            id = tgt.dataset.elementId;
        } else if (tgt.classList.contains("band-strip")) {
            target = "band";
            id = tgt.closest(".band")?.dataset?.bandKind || null;
        } else if (tgt.matches(".band-content[data-band-kind]")) {
            target = "band";
            id = tgt.dataset.bandKind;
        } else {
            target = "page";
        }
        state.dotnetRef.invokeMethodAsync("OnContextMenuRequest", target, id, ev.clientX, ev.clientY)
            .catch(err => console.warn("[designer] context menu open failed", err));
    });

    // Delegated dragstart for every toolbox item. The Razor markup emits
    // `<div class="tbx-item" draggable="true" data-kind="...">` so a single document-level
    // listener handles all items — present and future — without needing per-item interop.
    // Items marked `aria-disabled="true"` (the "(em breve)" placeholders) are skipped.
    document.addEventListener("dragstart", ev => {
        const item = ev.target.closest?.('.tbx-item[draggable="true"][data-kind]');
        if (!item) return;
        if (item.getAttribute("aria-disabled") === "true") { ev.preventDefault(); return; }
        ev.dataTransfer.setData("application/x-omni-toolbox-kind", item.dataset.kind);
        ev.dataTransfer.effectAllowed = "copy";
        item.classList.add("is-drag-source");
    });
    document.addEventListener("dragend", ev => {
        const item = ev.target.closest?.('.tbx-item');
        if (item) item.classList.remove("is-drag-source");
    });

    // Delegated dragstart for fields in DataSourcesTree. Reads data-field-name + companion
    // data-source-name/data-ambiguous from the DOM each drag so renames take effect without
    // a re-attach. The qualified form "Source.Field" only flies when ambiguous; otherwise
    // single-source reports keep their natural short "Field" token.
    document.addEventListener("dragstart", ev => {
        const item = ev.target?.closest?.('.is-field[draggable="true"][data-field-name]');
        if (!item) return;
        const fieldName = item.dataset.fieldName;
        const sourceName = item.dataset.sourceName || "";
        const ambiguous = item.dataset.ambiguous === "1";
        // Token that the .NET drop handler will wrap inside {Fields.<token>}.
        const token = ambiguous && sourceName ? `${sourceName}.${fieldName}` : fieldName;
        ev.dataTransfer.setData("application/x-omni-field", token);
        // Always include the source name so receivers (selection, smart inserts) know the origin
        // even in unambiguous drops.
        if (sourceName) ev.dataTransfer.setData("application/x-omni-field-source", sourceName);
        ev.dataTransfer.effectAllowed = "copy";
    });

    function setupCanvasDropZone(bandContent, bandKind) {
        const accepts = t => t.includes("application/x-omni-toolbox-kind")
                          || t.includes("application/x-omni-field");

        bandContent.addEventListener("dragover", ev => {
            if (accepts(ev.dataTransfer.types)) {
                ev.preventDefault();
                ev.dataTransfer.dropEffect = "copy";
                bandContent.classList.add("is-drop-target");
            }
        });
        bandContent.addEventListener("dragleave", () => bandContent.classList.remove("is-drop-target"));
        bandContent.addEventListener("drop", ev => {
            ev.preventDefault();
            bandContent.classList.remove("is-drop-target");

            const rect = bandContent.getBoundingClientRect();
            const x = ev.clientX - rect.left;
            const y = ev.clientY - rect.top;

            const kind = ev.dataTransfer.getData("application/x-omni-toolbox-kind");
            const field = ev.dataTransfer.getData("application/x-omni-field");

            if (state.dotnetRef && kind) {
                state.dotnetRef.invokeMethodAsync("OnToolboxDrop", {
                    kind, bandKind, xMm: px2mm(x), yMm: px2mm(y),
                }).catch(err => console.warn("[designer] toolbox drop failed", err));
            } else if (state.dotnetRef && field) {
                state.dotnetRef.invokeMethodAsync("OnFieldDrop", {
                    fieldName: field, bandKind, xMm: px2mm(x), yMm: px2mm(y),
                }).catch(err => console.warn("[designer] field drop failed", err));
            }
        });
    }

    function setupFieldDrag(element, fieldName) {
        element.setAttribute("draggable", "true");
        element.addEventListener("dragstart", ev => {
            ev.dataTransfer.setData("application/x-omni-field", fieldName);
            ev.dataTransfer.effectAllowed = "copy";
        });
    }

    // ───── 2b. Band resize (drag bottom edge) ───────────────────────────────────
    function setupBandResize(handle, bandKind) {
        // Pointer events MUST be captured by the handle, but it overlaps band-content's
        // position:relative box. Set inline z-index to win the stacking battle.
        handle.style.zIndex = "20";
        handle.style.cursor = "ns-resize";

        // Visual feedback on hover so users discover the resize affordance.
        handle.addEventListener("pointerenter", () => {
            handle.style.background = "var(--selection-soft, rgba(59,130,246,0.10))";
            handle.style.boxShadow = "inset 0 -2px 0 var(--selection, #3B82F6)";
        });
        handle.addEventListener("pointerleave", () => {
            if (!handle.classList.contains("is-dragging")) {
                handle.style.background = "transparent";
                handle.style.boxShadow = "none";
            }
        });

        handle.addEventListener("pointerdown", ev => {
            if (ev.button !== 0) return;
            ev.preventDefault();
            ev.stopPropagation();
            const bandEl = handle.closest(".band");
            if (!bandEl) {
                console.warn("[designer] band-resize handle has no .band ancestor");
                return;
            }
            handle.classList.add("is-dragging");
            handle.setPointerCapture?.(ev.pointerId);

            const startY = ev.clientY;
            // Read CSS-pixel height from inline style — zoom-independent. Using
            // getBoundingClientRect would give viewport pixels (scaled), causing band
            // resize to overshoot at zoom > 1.
            const origHeightPx = parseFloat(bandEl.style.height) || bandEl.getBoundingClientRect().height;

            // Floor: never shrink below the bottom of the lowest .el child. Matches the
            // server-side clamp in OnBandResize. Computing it on the client avoids letting
            // the user drag visually below the limit (which then fails to "snap back" if
            // the C# state value didn't change and Blazor skips the re-render diff).
            function computeFloorPx() {
                let floor = 2 * mm;
                bandEl.querySelectorAll(":scope > .band-content > .el").forEach(el => {
                    const top = parseFloat(el.style.top) || 0;
                    const h   = parseFloat(el.style.height) || 0;
                    if (top + h > floor) floor = top + h;
                });
                return floor;
            }

            function onMove(e) {
                const dy = (e.clientY - startY) / getCanvasZoom();
                const nh = Math.max(computeFloorPx(), origHeightPx + dy);
                bandEl.style.height = `${nh}px`;
            }
            function onUp() {
                document.removeEventListener("pointermove", onMove);
                document.removeEventListener("pointerup",   onUp);
                handle.classList.remove("is-dragging");
                handle.style.background = "transparent";
                handle.style.boxShadow = "none";
                // Read CSS pixels from inline style — same reason as origHeightPx.
                const finalCss = parseFloat(bandEl.style.height) || (bandEl.getBoundingClientRect().height / getCanvasZoom());
                if (state.dotnetRef) {
                    state.dotnetRef.invokeMethodAsync("OnBandResize", {
                        bandKind, heightMm: px2mm(finalCss),
                    }).catch(err => console.warn("[designer] band resize commit failed", err));
                }
            }
            document.addEventListener("pointermove", onMove);
            document.addEventListener("pointerup",   onUp, { once: true });
        });
    }

    // ───── 2c. Marquee multi-select on the page background ─────────────────────
    // Marquee fires whenever the user clicks on empty space — that includes the page
    // background, the margins guide, AND empty area INSIDE any band-content. The only
    // targets that block the marquee are actual elements (.el) and the band-resize
    // handle (which has its own gesture) and the band-strip label.
    function setupPageMarquee(pageEl) {
        let marquee = null;
        let startX = 0, startY = 0;
        let started = false;

        function isEmptyCanvasTarget(target) {
            if (!target) return false;
            // Climb a few levels to see if we're inside an interactive element first.
            let el = target;
            while (el && el !== pageEl) {
                if (el.classList) {
                    if (el.classList.contains("el")) return false;              // element body
                    if (el.classList.contains("handle")) return false;          // resize handle
                    if (el.classList.contains("band-strip")) return false;      // label strip
                    if (el.classList.contains("band-resize")) return false;     // band-bottom resize
                    if (el.classList.contains("fx-badge")) return false;
                    // SubDetail "Data Member" caption row — has a <select> dropdown that
                    // must receive its native click. Without this exclusion the page-level
                    // pointerdown handler calls preventDefault and the dropdown never opens.
                    if (el.classList.contains("band-caption-row")) return false;
                }
                // Also exempt anything inside a native <select> / <option> / <optgroup> —
                // those are interactive controls regardless of which container they live in.
                if (el.tagName === "SELECT" || el.tagName === "OPTION" || el.tagName === "OPTGROUP") {
                    return false;
                }
                el = el.parentElement;
            }
            // Allow start on the page surface, page-margins overlay, band itself,
            // or band-content (the band's interior).
            return true;
        }

        pageEl.addEventListener("pointerdown", ev => {
            if (ev.button !== 0) return;
            if (!isEmptyCanvasTarget(ev.target)) return;
            ev.preventDefault();

            // The marquee div lives inside pageEl, which is inside the scaled .canvas-stage,
            // so the marquee renders scaled. Track positions in CSS pixels (viewport / zoom)
            // so style.left/top/width/height we assign match the cursor visually at any zoom.
            const rect = pageEl.getBoundingClientRect();
            const z = getCanvasZoom();
            startX = (ev.clientX - rect.left) / z;
            startY = (ev.clientY - rect.top) / z;
            started = false; // becomes true once the user moves more than 3 px (CSS)

            document.addEventListener("pointermove", onMarqueeMove);
            document.addEventListener("pointerup",   onMarqueeUp, { once: true });
        });

        function ensureMarquee() {
            if (marquee) return;
            marquee = document.createElement("div");
            marquee.className = "marquee";
            marquee.style.cssText =
                `position:absolute;left:${startX}px;top:${startY}px;width:0;height:0;` +
                `border:1px dashed var(--selection,#3B82F6);` +
                `background:var(--selection-soft,rgba(59,130,246,0.10));` +
                `pointer-events:none;z-index:35;`;
            pageEl.appendChild(marquee);
        }

        function onMarqueeMove(e) {
            const rect = pageEl.getBoundingClientRect();
            const z = getCanvasZoom();
            const x = (e.clientX - rect.left) / z;
            const y = (e.clientY - rect.top) / z;
            const dx = Math.abs(x - startX);
            const dy = Math.abs(y - startY);
            if (!started) {
                if (dx < 3 && dy < 3) return; // ignore micro-jiggle clicks
                started = true;
                ensureMarquee();
            }
            const left = Math.min(x, startX);
            const top  = Math.min(y, startY);
            const w = Math.abs(x - startX);
            const h = Math.abs(y - startY);
            marquee.style.left = `${left}px`;
            marquee.style.top  = `${top}px`;
            marquee.style.width  = `${w}px`;
            marquee.style.height = `${h}px`;
        }

        function onMarqueeUp(ev) {
            document.removeEventListener("pointermove", onMarqueeMove);
            // Even very small drags (3-5 px) ended with the browser firing a synthesised
            // `click` event AFTER pointerup. That click bubbles into Blazor's @onclick on
            // .band-content / .page → ClearSelection runs → erases the selection we just
            // computed. Swallow exactly one upcoming click on this page.
            if (started) {
                let consumed = false;
                const swallow = (e) => {
                    if (consumed) return;
                    consumed = true;
                    e.stopPropagation();
                    e.preventDefault();
                    pageEl.removeEventListener("click", swallow, true);
                };
                pageEl.addEventListener("click", swallow, true);
                // If the browser doesn't fire a click (drag was long enough to cancel it),
                // remove the handler after a short window so it doesn't eat a legitimate
                // later click. 100ms is well past Chrome/Firefox's click-synthesis delay.
                setTimeout(() => {
                    if (!consumed) pageEl.removeEventListener("click", swallow, true);
                }, 100);
            }
            if (!started) {
                // Empty pointerdown without drag → clear selection (canvas click).
                if (state.dotnetRef) {
                    state.dotnetRef.invokeMethodAsync("OnMarqueeSelect", [])
                        .catch(() => {});
                }
                return;
            }
            if (!marquee) return;

            const rect = marquee.getBoundingClientRect();
            const ids = [];
            pageEl.querySelectorAll(".el[data-element-id]").forEach(el => {
                const er = el.getBoundingClientRect();
                // Element counts as selected if it INTERSECTS the marquee rect.
                if (er.right >= rect.left && er.left <= rect.right &&
                    er.bottom >= rect.top && er.top <= rect.bottom) {
                    ids.push(el.dataset.elementId);
                }
            });
            marquee.remove();
            marquee = null;
            started = false;
            if (state.dotnetRef) {
                state.dotnetRef.invokeMethodAsync("OnMarqueeSelect", ids)
                    .catch(err => console.warn("[designer] marquee select failed", err));
            }
        }
    }

    // ───── 2e. Outside-click listener for dropdown menus ───────────────────────
    const openDropdowns = new Set();
    function setupOutsideClick() {
        if (setupOutsideClick._installed) return;
        setupOutsideClick._installed = true;
        document.addEventListener("pointerdown", ev => {
            // If the click hits an open dropdown or context menu, let its own handler run.
            let el = ev.target;
            while (el) {
                if (el.classList && (el.classList.contains("dropdown-host") ||
                                     el.classList.contains("dropdown-panel") ||
                                     el.classList.contains("ctx-menu"))) {
                    return;
                }
                el = el.parentElement;
            }
            // Click was outside any open dropdown → close them all.
            const refs = Array.from(openDropdowns);
            openDropdowns.clear();
            refs.forEach(ref => ref.invokeMethodAsync("CloseFromOutside").catch(() => {}));
        }, true);
    }

    // ───── 3. Theme toggle ──────────────────────────────────────────────────────
    function setTheme(theme) {
        document.documentElement.setAttribute("data-theme", theme || "light");
    }

    function getTheme() {
        return document.documentElement.getAttribute("data-theme") || "light";
    }

    // Binds resize handles (.handle.nw, .n, .ne, .e, .se, .s, .sw, .w) inside an element.
    // Idempotent — each handle is flagged after binding so MutationObserver re-triggers don't
    // stack listeners. Handles are dynamic: they only exist while the element is selected.
    function bindHandles(element, opts) {
        element.querySelectorAll(".handle").forEach(h => {
            if (h.dataset.omniHandleBound) return;
            const dir = (h.className.match(/handle\s+(nw|n|ne|e|se|s|sw|w)/) || [])[1];
            if (!dir) return;
            h.dataset.omniHandleBound = "1";
            h.addEventListener("pointerdown", ev => {
                ev.stopPropagation();
                if (ev.button !== 0) return;
                if (opts.locked) return;
                startElementResize(ev, element, dir, opts);
            });
        });
    }

    // ───── 3.x. Panel splitters (resize left/right panels by dragging the .split-handle) ─
    //
    // The workspace is a CSS Grid `grid-template-columns: <left> 1fr <right>` (defaults
    // 240px / 1fr / 300px). Each panel has a `.split-handle.left|.right` strip that the user
    // grabs to resize. We override `grid-template-columns` inline on the workspace element
    // and persist the width in localStorage so layout choices survive page reloads.
    //
    // Why workspace-level (not panel-level width)? CSS Grid resolves the 1fr middle column
    // from the remaining space — pushing the side panels via grid-template avoids any race
    // between the panel's own width and the canvas region.
    const SPLITTER_STORAGE_KEY = "omniDesigner.splitterWidths";
    const SPLITTER_MIN = 180;   // px — below this the panel becomes effectively unusable
    const SPLITTER_MAX_PCT = 0.45; // never let one side panel exceed 45% of the viewport
    function readSplitterStorage() {
        try {
            const raw = localStorage.getItem(SPLITTER_STORAGE_KEY);
            if (!raw) return null;
            const parsed = JSON.parse(raw);
            return (parsed && typeof parsed === "object") ? parsed : null;
        } catch { return null; }
    }
    function writeSplitterStorage(widths) {
        try { localStorage.setItem(SPLITTER_STORAGE_KEY, JSON.stringify(widths)); }
        catch { /* quota / privacy mode — non-fatal */ }
    }
    function applySplitterWidths(workspace, left, right) {
        workspace.style.gridTemplateColumns = `${left}px 1fr ${right}px`;
    }
    function currentSplitterWidths(workspace) {
        // Parse the live computed columns rather than the inline style — gives correct
        // numbers even on first drag before any inline override.
        const cs = getComputedStyle(workspace);
        const cols = cs.gridTemplateColumns.split(/\s+/).filter(Boolean);
        const left = parseFloat(cols[0]) || 240;
        const right = parseFloat(cols[cols.length - 1]) || 300;
        return { left, right };
    }
    function bootstrapSplitterWidths() {
        const workspace = document.querySelector(".workspace");
        if (!workspace) return;
        const saved = readSplitterStorage();
        if (saved && Number.isFinite(saved.left) && Number.isFinite(saved.right)) {
            applySplitterWidths(workspace, saved.left, saved.right);
        }
    }
    // Restore persisted widths before the first paint — even if delegation runs later.
    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", bootstrapSplitterWidths, { once: true });
    } else {
        bootstrapSplitterWidths();
    }
    // Delegated splitter drag — works for any .split-handle that exists or appears later
    // (Blazor re-renders the panels often). Single document-level listener, idempotent.
    if (!document.body.dataset.omniSplitterBound) {
        document.body.dataset.omniSplitterBound = "1";
        document.addEventListener("pointerdown", ev => {
            const handle = ev.target?.closest?.(".split-handle");
            if (!handle || ev.button !== 0) return;
            const workspace = document.querySelector(".workspace");
            if (!workspace) return;
            ev.preventDefault();
            ev.stopPropagation();
            const side = handle.classList.contains("left") ? "left" : "right";
            const { left: leftStart, right: rightStart } = currentSplitterWidths(workspace);
            const startX = ev.clientX;
            const maxPx = window.innerWidth * SPLITTER_MAX_PCT;
            handle.classList.add("is-dragging");
            document.body.classList.add("is-splitting");

            // Capture pointer so we still receive move events even when the cursor escapes
            // the thin handle hot-zone (common when dragging fast).
            try { handle.setPointerCapture(ev.pointerId); } catch { /* old browsers */ }

            const onMove = e => {
                const dx = e.clientX - startX;
                let nextLeft = leftStart;
                let nextRight = rightStart;
                if (side === "right") {
                    // Right edge of the LEFT panel — moving right grows the panel.
                    nextLeft = Math.max(SPLITTER_MIN, Math.min(maxPx, leftStart + dx));
                } else {
                    // Left edge of the RIGHT panel — moving right shrinks the right panel.
                    nextRight = Math.max(SPLITTER_MIN, Math.min(maxPx, rightStart - dx));
                }
                applySplitterWidths(workspace, nextLeft, nextRight);
            };
            const onUp = () => {
                document.removeEventListener("pointermove", onMove);
                document.removeEventListener("pointerup", onUp);
                document.removeEventListener("pointercancel", onUp);
                handle.classList.remove("is-dragging");
                document.body.classList.remove("is-splitting");
                try { handle.releasePointerCapture(ev.pointerId); } catch { /* ignore */ }
                writeSplitterStorage(currentSplitterWidths(workspace));
            };
            document.addEventListener("pointermove", onMove);
            document.addEventListener("pointerup", onUp, { once: true });
            document.addEventListener("pointercancel", onUp, { once: true });
        });

        // Double-click resets the involved side to its default — matches VS Code / GitLens UX.
        document.addEventListener("dblclick", ev => {
            const handle = ev.target?.closest?.(".split-handle");
            if (!handle) return;
            const workspace = document.querySelector(".workspace");
            if (!workspace) return;
            const side = handle.classList.contains("left") ? "left" : "right";
            const cur = currentSplitterWidths(workspace);
            applySplitterWidths(workspace,
                side === "right" ? 240 : cur.left,
                side === "left"  ? 300 : cur.right);
            writeSplitterStorage(currentSplitterWidths(workspace));
        });
    }

    // ───── 3.y. Collapsible groups (toolbox + property grid sections + sidebar trees) ───
    //
    // Many UI groups have a `.chev` (chevron) inside their header. Clicking the header
    // should toggle visibility of the next-sibling content container. The Razor templates
    // render the chevrons but never wired the click — this delegated listener fills the gap
    // for every existing or future header without per-component plumbing.
    //
    // Convention: a "collapsible header" is any element with class `.tb-group-h`,
    // `.prop-section-head`, `.outline-section-head`, OR `.ds-source-node`. Adding the class
    // `is-collapsed` to the header hides every sibling up to the next header (CSS rule below).
    if (!document.body.dataset.omniCollapsibleBound) {
        document.body.dataset.omniCollapsibleBound = "1";
        const COLLAPSE_SELECTORS = [".tb-group-h", ".prop-section-head", ".outline-section-head", ".ds-source-node"];
        document.addEventListener("click", ev => {
            // Don't swallow clicks on inner controls (buttons, inputs) inside the header.
            const interactive = ev.target?.closest?.("input, select, button:not(.collapse-toggle), textarea, a");
            if (interactive) {
                // Allow the row-edit button on data source nodes through — its own handler runs.
                if (!interactive.classList.contains("ds-row-btn")) return;
                return;
            }
            for (const sel of COLLAPSE_SELECTORS) {
                const head = ev.target?.closest?.(sel);
                if (!head) continue;
                head.classList.toggle("is-collapsed");
                // Reflect on the chevron icon: down → right when collapsed.
                const chev = head.querySelector(".chev");
                if (chev) {
                    chev.style.transform = head.classList.contains("is-collapsed")
                        ? "rotate(-90deg)" : "";
                }
                return;
            }
        });
    }

    // ───── 3.z. Rulers (horizontal + vertical) ─────────────────────────────────
    // Viewport-fixed canvas rulers, the standard report-designer behaviour (DevExpress,
    // Telerik, Stimulsoft): they don't scroll with the page — instead, on every scroll /
    // zoom / resize / mouse-move they MEASURE the live page element (#designerPage) and
    // redraw ticks so "0 cm" sits exactly on the paper's top-left corner wherever it is on
    // screen. This makes them correct under any zoom, scroll position, and the page's
    // centered (margin:auto) layout. Features: 1 cm majors with cm labels, 2 mm minors, a
    // live mouse-position marker on both axes, and shading of the current selection's extent.
    const RULER_BASE_MM = 720 / 210; // px per mm at zoom 1 (matches --mm / --page-w)
    let rulerState = null;

    // Display units. major/minorMm = tick spacing; label() formats a mm value in the unit.
    const RULER_UNITS = {
        cm: { majorMm: 10,   minorMm: 2,        label: mm => trimNum(mm / 10),   abbr: "cm"  },
        mm: { majorMm: 10,   minorMm: 2,        label: mm => trimNum(mm),        abbr: "mm"  },
        in: { majorMm: 25.4, minorMm: 25.4 / 8, label: mm => trimNum(mm / 25.4), abbr: "pol" },
    };
    function trimNum(v) { return String(Math.round(v * 100) / 100); }

    function cssVarValue(name, fallback) {
        const v = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
        return v || fallback;
    }

    function setupRulers(hCanvas, vCanvas, scrollEl) {
        if (!scrollEl) return;
        // Re-attach cleanly if called again for a different scroll host (e.g. component remount).
        if (rulerState && rulerState.scrollEl === scrollEl) { rulerState.schedule(); return; }
        teardownRulers();

        const st = {
            hCanvas, vCanvas, scrollEl,
            mouseX: null, mouseY: null, raf: 0,
            unit: "cm",
            guides: [],   // [{ axis:'v'|'h', mm, el }]
            tip: null,
        };

        function schedule() {
            if (st.raf) return;
            st.raf = requestAnimationFrame(() => { st.raf = 0; drawAll(); });
        }

        function pageEl() { return document.getElementById("designerPage"); }
        function pageRect() { const p = pageEl(); return p ? p.getBoundingClientRect() : null; }
        function pxPerMm() { return RULER_BASE_MM * getCanvasZoom(); }

        function clientToPageMm(clientX, clientY) {
            const pr = pageRect();
            if (!pr) return null;
            const k = pxPerMm();
            return { x: (clientX - pr.left) / k, y: (clientY - pr.top) / k };
        }
        function pageSizeMm() {
            const pr = pageRect(); const k = pxPerMm();
            return pr ? { w: pr.width / k, h: pr.height / k } : { w: 0, h: 0 };
        }

        // ── Guides (dragged out from the rulers; live in #designerPage page-space) ──
        function positionGuide(g) {
            if (!g.el) return;
            const px = g.mm * RULER_BASE_MM; // unscaled page px — the stage transform scales it
            if (g.axis === "v") g.el.style.left = px + "px";
            else g.el.style.top = px + "px";
        }
        function bindGuide(g) {
            g.el.addEventListener("pointerdown", ev => {
                if (ev.button !== 0) return;
                ev.preventDefault();
                ev.stopPropagation(); // keep the page marquee from starting
                startGuideDrag(g, ev);
            });
            g.el.addEventListener("dblclick", ev => { ev.stopPropagation(); removeGuide(g); schedule(); });
        }
        // Self-healing render: recreates a guide div only if Blazor's re-render removed it.
        function renderGuides() {
            const p = pageEl();
            if (!p) return;
            for (const g of st.guides) {
                if (!g.el || !g.el.isConnected || g.el.parentElement !== p) {
                    g.el = document.createElement("div");
                    g.el.className = "omni-guide " + g.axis;
                    p.appendChild(g.el);
                    bindGuide(g);
                }
                positionGuide(g);
            }
        }
        function removeGuide(g) {
            const i = st.guides.indexOf(g);
            if (i >= 0) st.guides.splice(i, 1);
            g.el?.remove();
            hideTip();
        }
        function startGuideDrag(g, ev) {
            const onMove = e => {
                const m = clientToPageMm(e.clientX, e.clientY);
                if (!m) return;
                const size = pageSizeMm();
                let v = g.axis === "v" ? m.x : m.y;
                v = Math.max(0, Math.min(v, g.axis === "v" ? size.w : size.h));
                g.mm = v;
                positionGuide(g);
                showTip(g, e.clientX, e.clientY);
            };
            const onUp = e => {
                document.removeEventListener("pointermove", onMove);
                hideTip();
                const pr = pageRect();
                // Drag a guide off the relevant paper edge to delete it (Photoshop behaviour).
                const kill = !pr || (g.axis === "v"
                    ? (e.clientX < pr.left - 6 || e.clientX > pr.right + 6)
                    : (e.clientY < pr.top - 6 || e.clientY > pr.bottom + 6));
                if (kill) removeGuide(g);
                schedule();
            };
            document.addEventListener("pointermove", onMove);
            document.addEventListener("pointerup", onUp, { once: true });
        }
        // Pointer-down on a ruler creates a guide (perpendicular axis) and starts dragging it.
        // A plain click (no drag) drops it at the clicked coordinate.
        function beginGuideCreate(axis, ev) {
            if (ev.button !== 0) return;
            ev.preventDefault();
            const m = clientToPageMm(ev.clientX, ev.clientY);
            const g = { axis, mm: m ? Math.max(0, axis === "v" ? m.x : m.y) : 0, el: null };
            st.guides.push(g);
            renderGuides();
            startGuideDrag(g, ev);
            showTip(g, ev.clientX, ev.clientY);
        }

        function showTip(g, clientX, clientY) {
            if (!st.tip) {
                st.tip = document.createElement("div");
                st.tip.className = "omni-guide-tip";
                document.body.appendChild(st.tip);
            }
            const u = RULER_UNITS[st.unit] || RULER_UNITS.cm;
            st.tip.textContent = u.label(g.mm) + " " + u.abbr;
            st.tip.style.left = (clientX + 14) + "px";
            st.tip.style.top = (clientY + 14) + "px";
            st.tip.style.display = "block";
        }
        function hideTip() { if (st.tip) st.tip.style.display = "none"; }

        // ── Drawing ─────────────────────────────────────────────────────────────
        function selectionSpan(isH) {
            const els = document.querySelectorAll("#designerPage .el.is-selected");
            if (!els.length) return null;
            let lo = Infinity, hi = -Infinity;
            els.forEach(el => {
                const r = el.getBoundingClientRect();
                if (isH) { lo = Math.min(lo, r.left); hi = Math.max(hi, r.right); }
                else { lo = Math.min(lo, r.top); hi = Math.max(hi, r.bottom); }
            });
            return hi > lo ? { lo, hi } : null;
        }

        function drawAll() {
            sizeZoomBox();   // reserva (via margem) a caixa escalada p/ o scroll no zoom
            const pr = pageRect();
            const k = pxPerMm();
            drawAxis(hCanvas, true, pr, k);
            drawAxis(vCanvas, false, pr, k);
            renderGuides();
        }

        function drawAxis(canvas, isH, pr, k) {
            if (!canvas) return;
            const rect = canvas.getBoundingClientRect();
            if (rect.width < 1 || rect.height < 1) return; // hidden (e.g. preview mode)
            const dpr = window.devicePixelRatio || 1;
            canvas.width = Math.round(rect.width * dpr);
            canvas.height = Math.round(rect.height * dpr);
            const ctx = canvas.getContext("2d");
            if (!ctx) return;
            ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
            ctx.clearRect(0, 0, rect.width, rect.height);

            const thickness = isH ? rect.height : rect.width;
            const lenPx = isH ? rect.width : rect.height;
            const canvasStart = isH ? rect.left : rect.top;
            const pageStart = pr ? (isH ? pr.left : pr.top) : canvasStart;
            const originPx = pageStart - canvasStart; // local px where page coordinate 0 sits

            // Selection extent shading (drawn first, behind the ticks).
            const span = selectionSpan(isH);
            if (span) {
                ctx.fillStyle = cssVarValue("--selection-soft", "rgba(59,130,246,0.14)");
                const a = span.lo - canvasStart, b = span.hi - canvasStart;
                if (isH) ctx.fillRect(a, 0, b - a, thickness);
                else ctx.fillRect(0, a, thickness, b - a);
            }

            const u = RULER_UNITS[st.unit] || RULER_UNITS.cm;
            const tickColor = cssVarValue("--ruler-tick", "#A8A395");
            const textColor = cssVarValue("--ruler-text", "#6B675D");
            ctx.strokeStyle = tickColor;
            ctx.lineWidth = 1;
            ctx.font = "8px ui-monospace, SFMono-Regular, Menlo, Consolas, monospace";
            ctx.textBaseline = "alphabetic";

            // Iterate by integer minor-tick index so inch fractions stay exact.
            const ratio = Math.max(1, Math.round(u.majorMm / u.minorMm));
            const i0 = Math.floor(((0 - originPx) / k) / u.minorMm) - 1;
            const i1 = Math.ceil(((lenPx - originPx) / k) / u.minorMm) + 1;

            ctx.beginPath();
            for (let i = i0; i <= i1; i++) {
                const p = originPx + (i * u.minorMm) * k;
                if (p < -1 || p > lenPx + 1) continue;
                const major = (i % ratio) === 0;
                const tl = major ? thickness * 0.55 : thickness * 0.30;
                const q = Math.round(p) + 0.5; // crisp 1px line
                if (isH) { ctx.moveTo(q, thickness); ctx.lineTo(q, thickness - tl); }
                else { ctx.moveTo(thickness, q); ctx.lineTo(thickness - tl, q); }
            }
            ctx.stroke();

            // Labels at non-negative majors, formatted in the active unit.
            ctx.fillStyle = textColor;
            for (let i = i0; i <= i1; i++) {
                if (i % ratio !== 0) continue;
                const mmv = i * u.minorMm;
                if (mmv < 0) continue;
                const p = originPx + mmv * k;
                if (p < 6 || p > lenPx) continue;
                const label = u.label(mmv);
                if (isH) {
                    ctx.fillText(label, Math.round(p) + 2, 9);
                } else {
                    ctx.save();
                    ctx.translate(9, Math.round(p) - 2);
                    ctx.rotate(-Math.PI / 2);
                    ctx.fillText(label, 0, 0);
                    ctx.restore();
                }
            }

            // Live mouse-position marker.
            const mpos = isH ? st.mouseX : st.mouseY;
            if (mpos != null) {
                const local = mpos - canvasStart;
                if (local >= 0 && local <= lenPx) {
                    ctx.strokeStyle = cssVarValue("--accent", "#C2410C");
                    ctx.beginPath();
                    const q = Math.round(local) + 0.5;
                    if (isH) { ctx.moveTo(q, 0); ctx.lineTo(q, thickness); }
                    else { ctx.moveTo(0, q); ctx.lineTo(thickness, q); }
                    ctx.stroke();
                }
            }
        }

        st.schedule = schedule;
        st.draw = drawAll;
        st.onScroll = () => schedule();
        st.onMove = e => { st.mouseX = e.clientX; st.mouseY = e.clientY; schedule(); };
        st.onLeave = () => { st.mouseX = null; st.mouseY = null; schedule(); };
        st.onWinResize = () => schedule();
        st.onHDown = e => beginGuideCreate("v", e); // horizontal ruler → vertical guide
        st.onVDown = e => beginGuideCreate("h", e); // vertical ruler → horizontal guide

        scrollEl.addEventListener("scroll", st.onScroll, { passive: true });
        scrollEl.addEventListener("pointermove", st.onMove, { passive: true });
        scrollEl.addEventListener("pointerleave", st.onLeave, { passive: true });
        window.addEventListener("resize", st.onWinResize, { passive: true });
        if (hCanvas) { hCanvas.addEventListener("pointerdown", st.onHDown); hCanvas.style.cursor = "ew-resize"; }
        if (vCanvas) { vCanvas.addEventListener("pointerdown", st.onVDown); vCanvas.style.cursor = "ns-resize"; }
        if (typeof ResizeObserver !== "undefined") {
            st.ro = new ResizeObserver(() => schedule());
            st.ro.observe(scrollEl);
            if (hCanvas) st.ro.observe(hCanvas);
            if (vCanvas) st.ro.observe(vCanvas);
        }
        rulerState = st;
        schedule();
    }

    function teardownRulers() {
        const st = rulerState;
        if (!st) return;
        try {
            st.scrollEl.removeEventListener("scroll", st.onScroll);
            st.scrollEl.removeEventListener("pointermove", st.onMove);
            st.scrollEl.removeEventListener("pointerleave", st.onLeave);
            window.removeEventListener("resize", st.onWinResize);
            st.hCanvas?.removeEventListener("pointerdown", st.onHDown);
            st.vCanvas?.removeEventListener("pointerdown", st.onVDown);
            st.ro?.disconnect();
            if (st.raf) cancelAnimationFrame(st.raf);
            st.guides.forEach(g => g.el?.remove());
            st.tip?.remove();
        } catch { /* best-effort cleanup */ }
        rulerState = null;
    }

    // ───── 4. Public API ────────────────────────────────────────────────────────
    window.omniDesigner = {
        register(dotnetRef) { state.dotnetRef = dotnetRef; },
        unregister() { state.dotnetRef = null; },

        attachElement(element, options) {
            if (!element) return;
            const opts = options || { snap: true, snapMm: 1, locked: false };
            // Element-level pointerdown: idempotent — guard with a dataset flag so re-calls
            // (after Blazor re-renders the element subtree) don't stack duplicate listeners.
            if (!element.dataset.omniMoveBound) {
                element.dataset.omniMoveBound = "1";
                element.addEventListener("pointerdown", ev => {
                    if (ev.target.classList?.contains("handle")) return;
                    if (ev.button !== 0) return;
                    // Shift- or Ctrl/Cmd-click toggles multi-selection (industry standard).
                    // Both keys delegate to the same .NET method — selection semantics are
                    // identical from the server's perspective; the modifier just signals
                    // "add to selection" instead of "set selection".
                    if ((ev.shiftKey || ev.ctrlKey || ev.metaKey) && state.dotnetRef) {
                        state.dotnetRef.invokeMethodAsync("OnShiftClickElement", element.dataset.elementId)
                            .catch(err => console.warn("[designer] shift/ctrl-click failed", err));
                        return;
                    }
                    if (opts.locked) return; // locked elements ignore drag
                    startElementMove(ev, element, opts);
                });
            }
            // Handles only exist while the element is selected — Blazor creates them on demand.
            // Use a MutationObserver to bind whenever new .handle children appear; the observer
            // is itself one-per-element via a dataset flag.
            bindHandles(element, opts);
            if (!element.dataset.omniHandleObserver) {
                element.dataset.omniHandleObserver = "1";
                const mo = new MutationObserver(() => bindHandles(element, opts));
                mo.observe(element, { childList: true, subtree: true });
            }
        },

        attachToolbox(item, kind)               { setupToolboxDrag(item, kind); },
        attachField(element, fieldName)         { setupFieldDrag(element, fieldName); },
        attachDropZone(bandContent, bandKind)   { setupCanvasDropZone(bandContent, bandKind); },
        attachBandResize(handle, bandKind)      { setupBandResize(handle, bandKind); },
        attachPageMarquee(pageEl)               { setupPageMarquee(pageEl); },
        attachRulers(hCanvas, vCanvas, scrollEl){ setupRulers(hCanvas, vCanvas, scrollEl); },
        refreshRulers()                         { rulerState?.schedule?.(); },
        setRulerUnit(unit)                      { if (rulerState && RULER_UNITS[unit]) { rulerState.unit = unit; rulerState.schedule(); } },
        detachRulers()                          { teardownRulers(); },
        registerOutsideClick(dotnetRef) {
            setupOutsideClick();
            openDropdowns.add(dotnetRef);
        },

        setTheme,
        getTheme,
        toggleTheme() {
            const cur = getTheme();
            setTheme(cur === "dark" ? "light" : "dark");
            return getTheme();
        },

        // Focus shim — needed so global keyboard shortcuts work inside WebView.
        focus(rootEl) { rootEl?.focus?.(); },
    };
})();

// Auto-load the print sidecar module (designer-print.js) so hosts only need to
// reference designer.js. The sidecar registers `window.omniDesignerPrint`.
(function () {
    if (window.omniDesignerPrint) return;
    if (!document?.head) return;
    // Resolve our own script path so we can locate the sibling print module.
    // This works regardless of how the host references designer.js (RCL prefix,
    // CDN, fingerprinted path, etc.) — we look up the actual <script> element.
    const here = Array.from(document.querySelectorAll("script"))
        .map(s => s.src)
        .filter(s => s && s.endsWith("/js/designer.js"))[0];
    if (!here) return;
    const base = here.substring(0, here.lastIndexOf("/"));
    const tag = document.createElement("script");
    tag.src = base + "/designer-print.js";
    tag.async = true;
    document.head.appendChild(tag);
})();
