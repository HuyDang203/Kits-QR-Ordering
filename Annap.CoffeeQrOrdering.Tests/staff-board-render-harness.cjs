// Standalone Node.js harness that loads staff-orders-board.js with a minimal
// DOM stub, calls mergeBoard with crafted payloads, and inspects the column
// bodies to verify grouping-vs-individual card behavior.
//
// This is the regression check for the Column 3 grouping DOM-stale bug.
// Two classes of scenarios are exercised:
//
//   1. Single-step scenarios that load one payload and assert the resulting
//      DOM. These guard the initial-render shape of each grouping.
//
//   2. Multi-step scenarios that load several payloads in sequence through
//      the SAME FakeDocument, then assert the DOM after each step. These
//      guard the live transitions: drag-to-group, refresh-while-grouped,
//      and ungroup. The original "group A+B but old A and B are still
//      rendered as separate cards" bug ONLY manifests across multiple
//      mergeBoard() calls — a single-shot harness misses it.
//
// The script is run from the StaffOrderGroupingRenderTests integration
// test via `node` and asserted via the process exit code.

const fs = require("fs");
const path = require("path");
const vm = require("vm");

// ── Minimal DOM stub ──────────────────────────────────────────────────────
// staff-orders-board.js needs: querySelector, querySelectorAll, createElement,
// appendChild, removeChild, remove, addEventListener, setTimeout,
// getAttribute, classList (toggle, add, remove), innerHTML, firstElementChild,
// parentNode. We implement just enough to simulate one board with three
// column bodies, then inspect.

class FakeClassList {
    constructor() { this.set = new Set(); }
    add(...names) { names.forEach((n) => this.set.add(n)); }
    remove(...names) { names.forEach((n) => this.set.delete(n)); }
    toggle(name, force) {
        const has = this.set.has(name);
        const should = typeof force === "boolean" ? force : !has;
        if (should) this.set.add(name); else this.set.delete(name);
        return should;
    }
    contains(name) { return this.set.has(name); }
    toString() { return Array.from(this.set).join(" "); }
}

class FakeElement {
    constructor(tag) {
        this.tagName = tag.toUpperCase();
        this.children = [];
        this.attrs = {};
        this.dataset = {};
        this.classList = new FakeClassList();
        this.style = {};
        this.hidden = false;
        this.listeners = new Map();
        this._innerHTML = "";
        this.parentNode = null;
    }
    setAttribute(name, value) { this.attrs[name] = String(value); }
    getAttribute(name) { return Object.prototype.hasOwnProperty.call(this.attrs, name) ? this.attrs[name] : null; }
    removeAttribute(name) { delete this.attrs[name]; }
    appendChild(child) {
        if (child.nodeType === 11 /* document fragment proxy */) {
            // Unwrap the wrapper div used to parse innerHTML.
            for (const c of child.children) {
                c.parentNode = this;
                this.children.push(c);
            }
        } else {
            child.parentNode = this;
            this.children.push(child);
        }
        return child;
    }
    removeChild(child) {
        const idx = this.children.indexOf(child);
        if (idx >= 0) {
            this.children.splice(idx, 1);
            child.parentNode = null;
        }
        return child;
    }
    // Real browsers expose Element.remove(); mirror it so the production
    // code can call it directly. Without this, mountCard and mountGroupedCard
    // throw when removing stale cards across multi-step scenarios.
    remove() {
        if (this.parentNode) this.parentNode.removeChild(this);
    }
    insertBefore(child, before) {
        const idx = this.children.indexOf(before);
        if (idx < 0) return this.appendChild(child);
        child.parentNode = this;
        this.children.splice(idx, 0, child);
        return child;
    }
    get firstElementChild() {
        for (const c of this.children) if (c.tagName) return c;
        return null;
    }
    get innerHTML() { return this._innerHTML; }
    set innerHTML(value) {
        this._innerHTML = String(value);
        // Parse a single top-level element (the JS uses `wrap.firstElementChild`).
        this.children.length = 0;
        const match = /<([a-zA-Z0-9-]+)([^>]*)>([\s\S]*?)<\/\1>/.exec(this._innerHTML);
        if (match) {
            const el = new FakeElement(match[1]);
            this.children.push(el);
            el.parentNode = this;
            // Best-effort: capture data-* attributes from the open tag.
            const attrRe = /([a-zA-Z0-9-]+)="([^"]*)"/g;
            let m;
            while ((m = attrRe.exec(match[2])) !== null) {
                el.attrs[m[1]] = m[2];
                if (m[1].startsWith("data-")) el.dataset[m[1].slice(5)] = m[2];
            }
            const classMatch = /class="([^"]*)"/.exec(match[2]);
            if (classMatch) classMatch[1].split(/\s+/).filter(Boolean).forEach((c) => el.classList.add(c));
        }
    }
    querySelector(selector) {
        return querySelectorAll(this, selector)[0] || null;
    }
    querySelectorAll(selector) {
        return querySelectorAll(this, selector);
    }
    addEventListener(name, fn) {
        if (!this.listeners.has(name)) this.listeners.set(name, []);
        this.listeners.get(name).push(fn);
    }
    removeEventListener(name, fn) {
        if (!this.listeners.has(name)) return;
        const arr = this.listeners.get(name);
        const idx = arr.indexOf(fn);
        if (idx >= 0) arr.splice(idx, 1);
    }
}

function querySelectorAll(root, selector) {
    // Supports ".cls" (one or many), "[attr=value]" (one or many), and the
    // tag prefix "article" optionally. Multi-attribute selectors like
    // "[data-order-id=A][data-group-id=g1]" are required by
    // mountGroupedCard() in the production code, so they MUST work.
    const wantTag = (selector.match(/^([a-zA-Z0-9-]+)/) || [])[1];
    const wantClass = (selector.match(/\.([\w-]+)/g) || []).map((s) => s.slice(1));
    const wantAttrList = (selector.match(/\[([\w-]+)="([^"]*)"\]/g) || [])
        .map((s) => { const m = /\[([\w-]+)="([^"]*)"\]/.exec(s); return { name: m[1], value: m[2] }; });
    const out = [];
    const walk = (node) => {
        if (!node || !node.children) return;
        for (const c of node.children) {
            let match = true;
            if (wantTag && c.tagName && c.tagName.toLowerCase() !== wantTag.toLowerCase()) match = false;
            for (const cls of wantClass) if (!c.classList || !c.classList.contains(cls)) match = false;
            for (const wantAttr of wantAttrList) if (c.getAttribute(wantAttr.name) !== wantAttr.value) match = false;
            if (match) out.push(c);
            walk(c);
        }
    };
    walk(root);
    return out;
}

class FakeDocument extends FakeElement {
    constructor() {
        super("#document");
        this._boardEl = new FakeElement("div");
        this._submittedBody = new FakeElement("div");
        this._paidBody = new FakeElement("div");
        this._completedBody = new FakeElement("div");
        this.body = new FakeElement("body");
        this._boardEl.setAttribute("data-staff-board", "");
        this._submittedBody.setAttribute("data-staff-drop", "submitted");
        this._paidBody.setAttribute("data-staff-drop", "paid");
        this._completedBody.setAttribute("data-staff-drop", "completed");
        this._boardEl.appendChild(this._submittedBody);
        this._boardEl.appendChild(this._paidBody);
        this._boardEl.appendChild(this._completedBody);
        this.body.appendChild(this._boardEl);
        this.appendChild(this.body);
    }
    querySelector(selector) {
        if (selector === "[data-staff-board]") return this._boardEl;
        return super.querySelector(selector);
    }
    querySelectorAll(selector) {
        return super.querySelectorAll(selector);
    }
    createElement(tag) {
        return new FakeElement(tag);
    }
}

// ── Globals injected into the VM ──────────────────────────────────────────
const fakeWindow = {
    setTimeout,
    clearTimeout,
    setInterval,
    clearInterval,
    matchMedia: () => ({ matches: false, addEventListener() {}, removeEventListener() {} }),
    AnnapMoney: { format: (v) => String(v) },
    __annapApiUrl: (p) => p,
    __annapHubUrl: () => "/hubs/orders",
    addEventListener() {},
    history: { replaceState() {} },
    location: { hash: "", href: "" }
};

const jsPath = path.join(
    __dirname,
    "..",
    "Annap.CoffeeQrOrdering.Web",
    "wwwroot",
    "js",
    "staff-orders-board.js"
);
const src = fs.readFileSync(jsPath, "utf8");

// ── Helpers ──────────────────────────────────────────────────────────────
function makeOrder(id, opts = {}) {
    return {
        id,
        orderNumber: id.slice(0, 6),
        tableCode: opts.tableCode || "T36",
        staffStatus: opts.staffStatus || "submitted",
        boardColumn: opts.boardColumn || opts.staffStatus || "submitted",
        createdAtUtc: opts.createdAtUtc || new Date().toISOString(),
        updatedAtUtc: opts.updatedAtUtc || opts.createdAtUtc || new Date().toISOString(),
        statusChangedAtUtc: opts.createdAtUtc || new Date().toISOString(),
        paymentMethod: opts.paymentMethod || "Cash",
        paymentMethodLabelVi: "Tiền mặt",
        pendingPaymentBadgeVi: null,
        fulfillmentModeLabelVi: "Tại bàn",
        isTakeaway: false,
        totalAmount: opts.totalAmount || 30000,
        items: opts.items || [
            { id: "it-" + id, name: "Cafe đen", quantity: 1, unitPrice: 30000, preparedQuantity: 0 }
        ]
    };
}

function makeGroup(opts) {
    return {
        id: opts.id,
        primaryOrderId: opts.primaryOrderId,
        orderIds: opts.orderIds,
        primaryBoardColumn: opts.primaryBoardColumn || "submitted",
        tableCodes: opts.tableCodes || ["T36"],
        memberCount: opts.orderIds.length,
        aggregateTotal: opts.aggregateTotal || 60000
    };
}

function buildPayload(submittedOrders, paidOrders, completedOrders, groups) {
    return {
        youAre: "test-host",
        roles: ["Admin"],
        permissions: {
            canMarkPaid: true,
            canComplete: true,
            canPrepareItems: true,
            canManageBills: true,
            canGroup: true
        },
        submitted: submittedOrders,
        paid: paidOrders,
        completed: completedOrders,
        groups,
        active: [],
        recentServed: []
    };
}

// Each scenario runs in its own VM context with a fresh FakeDocument so DOM
// state cannot leak between scenarios. The helpers below expose the IIFE's
// inner `mergeBoard`, `lastBoardGroups`, etc. onto globalThis so the
// harness can drive mergeBoard synchronously and inspect the DOM.
function createSandbox() {
    const sb = {
        document: new FakeDocument(),
        window: { ...fakeWindow, fetch: async () => ({ ok: true, status: 200, headers: { get: () => null }, json: async () => ({}), text: async () => "" }) },
        setTimeout,
        clearTimeout,
        setInterval,
        clearInterval,
        console,
        performance: { now: () => Date.now() },
        Date,
        Math,
        JSON,
        Object,
        Array,
        Set,
        Map,
        Symbol,
        Error,
        TypeError,
        globalThis: null
    };
    sb.window.document = sb.document;
    sb.globalThis = sb;
    vm.createContext(sb);
    const exposedSrc = src.replace(
        "if (document.readyState === \"loading\") document.addEventListener(\"DOMContentLoaded\", boot);\n    else boot();",
        "globalThis.__staffBoardTest = { mergeBoard, syncColumn, lastBoardGroups: () => lastBoardGroups, lastBoardData: () => lastBoardData, buildCardHtml, buildGroupedCardHtml, mountCard, mountGroupedCard };"
    );
    vm.runInContext(exposedSrc, sb);
    return sb;
}

// ── Scenario driver: single-step ──────────────────────────────────────────
// Single-mergeBoard call. Used for the original "first render" assertions.
function runScenario(name, submitted, groups) {
    const sb = createSandbox();
    const testApi = sb.__staffBoardTest;
    if (!testApi || typeof testApi.mergeBoard !== "function") {
        console.log("FAIL harness: __staffBoardTest not exposed for " + name);
        process.exit(2);
    }

    const payload = buildPayload(submitted, [], [], groups);
    testApi.mergeBoard(payload);

    const board = sb.document._boardEl;
    const submittedBody = sb.document._submittedBody;
    const paidBody = sb.document._paidBody;
    const completedBody = sb.document._completedBody;
    const cards = querySelectorAll(board, ".staff-order-card");
    const groupedCards = querySelectorAll(board, ".staff-order-card--grouped");
    const normalCards = cards.filter((c) => !c.classList.contains("staff-order-card--grouped"));
    return {
        name,
        totalCards: cards.length,
        groupedCards: groupedCards.length,
        normalCards: normalCards.length,
        submittedCardCount: querySelectorAll(submittedBody, ".staff-order-card").length,
        paidCardCount: querySelectorAll(paidBody, ".staff-order-card").length,
        completedCardCount: querySelectorAll(completedBody, ".staff-order-card").length
    };
}

// ── Scenario driver: multi-step ───────────────────────────────────────────
// Calls mergeBoard repeatedly with the same FakeDocument. After each step
// we snapshot the DOM state into a result row, so the harness can assert
// the FULL lifecycle: drag → grouped → refresh → ungroup → regroup.
//
// This is the driver that would have caught the manual "Old A and Old B
// cards still visible after group A+B" bug — a single-mergeBoard driver
// always passes because it never creates the stale state.
function runMultiStepScenario(name, steps) {
    const sb = createSandbox();
    const testApi = sb.__staffBoardTest;
    if (!testApi || typeof testApi.mergeBoard !== "function") {
        console.log("FAIL harness: __staffBoardTest not exposed for " + name);
        process.exit(2);
    }

    const snapshots = [];
    for (const step of steps) {
        const payload = buildPayload(
            step.submitted || [],
            step.paid || [],
            step.completed || [],
            step.groups || []
        );
        testApi.mergeBoard(payload);

        // Snapshot every article inside the board, capturing its identity
        // (data-order-id + data-group-id) so we can detect duplicates and
        // stale cards.
        const cards = querySelectorAll(sb.document._boardEl, "article.staff-order-card");
        const cardDescriptors = cards.map((c) => ({
            orderId: c.getAttribute("data-order-id"),
            groupId: c.getAttribute("data-group-id"),
            grouped: c.classList.contains("staff-order-card--grouped"),
            column: c.getAttribute("data-board-column")
        }));

        // Compute duplicates: any (column, orderId) appearing more than once
        // when groupId is the same. Stale cards show up as duplicates of an
        // OrderId that's already represented by a grouped card.
        const seen = new Map();
        const duplicates = [];
        for (const d of cardDescriptors) {
            const key = `${d.column}|${d.orderId}|${d.groupId || ""}`;
            seen.set(key, (seen.get(key) || 0) + 1);
        }
        for (const [key, count] of seen.entries()) {
            if (count > 1) duplicates.push(key + " x" + count);
        }

        snapshots.push({
            label: step.label || "?",
            cardDescriptors,
            duplicates,
            groupedCount: cardDescriptors.filter((d) => d.grouped).length,
            standaloneCount: cardDescriptors.filter((d) => !d.grouped).length
        });
    }

    return { name, snapshots };
}

// ── Assertion helpers ────────────────────────────────────────────────────
// Snapshot equality: order-ids + group-ids + grouped-flag must match the
// expectation exactly (order-insensitive within the column).
function snapshotMatches(snapshot, expected) {
    const actualOrderIds = snapshot.cardDescriptors
        .filter((d) => d.column === "submitted")
        .map((d) => `${d.grouped ? "G" : "O"}:${d.orderId || "?"}${d.groupId ? `(${d.groupId})` : ""}`)
        .sort();
    const expectedSorted = [...expected].sort();
    return JSON.stringify(actualOrderIds) === JSON.stringify(expectedSorted);
}

function duplicatesAreEmpty(snapshot) {
    return snapshot.duplicates.length === 0;
}

// ── Test scenarios ───────────────────────────────────────────────────────
async function main() {
    const orderA = "00000000-0000-0000-0000-00000000000a";
    const orderB = "00000000-0000-0000-0000-00000000000b";
    const orderC = "00000000-0000-0000-0000-00000000000c";
    const orderD = "00000000-0000-0000-0000-00000000000d";
    const groupAB = "00000000-0000-0000-0000-0000000000ab";
    const groupCD = "00000000-0000-0000-0000-0000000000cd";
    const groupAC = "00000000-0000-0000-0000-0000000000ac";

    const A = makeOrder(orderA, { tableCode: "T36", createdAtUtc: "2026-09-27T10:00:00Z" });
    const B = makeOrder(orderB, { tableCode: "T36", createdAtUtc: "2026-09-27T10:01:00Z" });
    const C = makeOrder(orderC, { tableCode: "T37", createdAtUtc: "2026-09-27T10:02:00Z" });
    const D = makeOrder(orderD, { tableCode: "T38", createdAtUtc: "2026-09-27T10:03:00Z" });

    // ── Single-step scenarios (initial-render shape) ──────────────────────
    const singleResults = [];

    singleResults.push(await runScenario(
        "Group[A,B]",
        [A, B],
        [makeGroup({ id: groupAB, primaryOrderId: orderA, orderIds: [orderA, orderB], primaryBoardColumn: "submitted", tableCodes: ["T36"], aggregateTotal: 60000 })]
    ));

    singleResults.push(await runScenario(
        "Group[A,B,C]",
        [A, B, C],
        [makeGroup({ id: groupAB, primaryOrderId: orderA, orderIds: [orderA, orderB, orderC], primaryBoardColumn: "submitted", tableCodes: ["T36", "T37"], aggregateTotal: 90000 })]
    ));

    singleResults.push(await runScenario(
        "Group[A,B]+Group[C,D]",
        [A, B, C, D],
        [
            makeGroup({ id: groupAB, primaryOrderId: orderA, orderIds: [orderA, orderB], primaryBoardColumn: "submitted", tableCodes: ["T36"], aggregateTotal: 60000 }),
            makeGroup({ id: groupCD, primaryOrderId: orderC, orderIds: [orderC, orderD], primaryBoardColumn: "submitted", tableCodes: ["T37", "T38"], aggregateTotal: 90000 })
        ]
    ));

    singleResults.push(await runScenario(
        "Group[A,B]+C",
        [A, B, C],
        [makeGroup({ id: groupAB, primaryOrderId: orderA, orderIds: [orderA, orderB], primaryBoardColumn: "submitted", tableCodes: ["T36"], aggregateTotal: 60000 })]
    ));

    const sameTableA = makeOrder(orderA, { tableCode: "T36", createdAtUtc: "2026-09-27T10:00:00Z" });
    const sameTableB = makeOrder(orderB, { tableCode: "T36", createdAtUtc: "2026-09-27T10:01:00Z" });
    singleResults.push(await runScenario(
        "Group[A,B] sameTableCode",
        [sameTableA, sameTableB],
        [makeGroup({ id: groupAB, primaryOrderId: orderA, orderIds: [orderA, orderB], primaryBoardColumn: "submitted", tableCodes: ["T36"], aggregateTotal: 60000 })]
    ));

    singleResults.push(await runScenario(
        "Refresh Group[A,B]",
        [A, B],
        [makeGroup({ id: groupAB, primaryOrderId: orderA, orderIds: [orderA, orderB], primaryBoardColumn: "submitted", tableCodes: ["T36"], aggregateTotal: 60000 })]
    ));

    singleResults.push(await runScenario(
        "Ungrouped A,B",
        [A, B],
        []
    ));

    const singleExpectations = [
        { name: "Group[A,B]", groupedCards: 1, normalCards: 0, submittedCardCount: 1 },
        { name: "Group[A,B,C]", groupedCards: 1, normalCards: 0, submittedCardCount: 1 },
        { name: "Group[A,B]+Group[C,D]", groupedCards: 2, normalCards: 0, submittedCardCount: 2 },
        { name: "Group[A,B]+C", groupedCards: 1, normalCards: 1, submittedCardCount: 2 },
        { name: "Group[A,B] sameTableCode", groupedCards: 1, normalCards: 0, submittedCardCount: 1 },
        { name: "Refresh Group[A,B]", groupedCards: 1, normalCards: 0, submittedCardCount: 1 },
        { name: "Ungrouped A,B", groupedCards: 0, normalCards: 2, submittedCardCount: 2 }
    ];

    // ── Multi-step scenarios (lifecycle / stale-card detection) ───────────
    // The expectations are arrays of card-descriptor strings in the
    // submitted column, one per snapshot step. The order is the column-
    // sorted list of (grouped|standalone):OrderId(groupId) tokens.
    const multiExpectations = [];

    // Scenario A — manual "drag B onto A" in Column 3.
    // Step 1: A,B,C standalone → 3 cards. Step 2: group A+B → 1 grouped + 1 standalone C.
    const multiA = runMultiStepScenario("Multistep A: drag B onto A", [
        { label: "initial", submitted: [A, B, C], groups: [] },
        {
            label: "after-group",
            submitted: [A, B, C],
            groups: [makeGroup({ id: groupAB, primaryOrderId: orderA, orderIds: [orderA, orderB], primaryBoardColumn: "submitted", tableCodes: ["T36"], aggregateTotal: 60000 })]
        }
    ]);
    multiExpectations.push({
        name: "Multistep A",
        snapshots: multiA.snapshots,
        expected: [
            ["O:" + orderA, "O:" + orderB, "O:" + orderC],
            ["G:" + orderA + "(" + groupAB + ")", "O:" + orderC]
        ]
    });

    // Scenario B — group A+B, then refresh.
    const multiB = runMultiStepScenario("Multistep B: group then refresh", [
        { label: "initial", submitted: [A, B, C], groups: [] },
        {
            label: "after-group",
            submitted: [A, B, C],
            groups: [makeGroup({ id: groupAB, primaryOrderId: orderA, orderIds: [orderA, orderB], primaryBoardColumn: "submitted", tableCodes: ["T36"], aggregateTotal: 60000 })]
        },
        {
            label: "refresh-1",
            submitted: [A, B, C],
            groups: [makeGroup({ id: groupAB, primaryOrderId: orderA, orderIds: [orderA, orderB], primaryBoardColumn: "submitted", tableCodes: ["T36"], aggregateTotal: 60000 })]
        },
        {
            label: "refresh-2",
            submitted: [A, B, C],
            groups: [makeGroup({ id: groupAB, primaryOrderId: orderA, orderIds: [orderA, orderB], primaryBoardColumn: "submitted", tableCodes: ["T36"], aggregateTotal: 60000 })]
        }
    ]);
    multiExpectations.push({
        name: "Multistep B",
        snapshots: multiB.snapshots,
        expected: [
            ["O:" + orderA, "O:" + orderB, "O:" + orderC],
            ["G:" + orderA + "(" + groupAB + ")", "O:" + orderC],
            ["G:" + orderA + "(" + groupAB + ")", "O:" + orderC],
            ["G:" + orderA + "(" + groupAB + ")", "O:" + orderC]
        ]
    });

    // Scenario C — group A+B then ungroup.
    const multiC = runMultiStepScenario("Multistep C: group then ungroup", [
        { label: "initial", submitted: [A, B, C], groups: [] },
        {
            label: "after-group",
            submitted: [A, B, C],
            groups: [makeGroup({ id: groupAB, primaryOrderId: orderA, orderIds: [orderA, orderB], primaryBoardColumn: "submitted", tableCodes: ["T36"], aggregateTotal: 60000 })]
        },
        { label: "after-ungroup", submitted: [A, B, C], groups: [] }
    ]);
    multiExpectations.push({
        name: "Multistep C",
        snapshots: multiC.snapshots,
        expected: [
            ["O:" + orderA, "O:" + orderB, "O:" + orderC],
            ["G:" + orderA + "(" + groupAB + ")", "O:" + orderC],
            ["O:" + orderA, "O:" + orderB, "O:" + orderC]
        ]
    });

    // Scenario D — A,B,C,D then group A+B and group C+D (two groups, no
    // stragglers).
    const multiD = runMultiStepScenario("Multistep D: two independent groups", [
        { label: "initial", submitted: [A, B, C, D], groups: [] },
        {
            label: "group-AB",
            submitted: [A, B, C, D],
            groups: [makeGroup({ id: groupAB, primaryOrderId: orderA, orderIds: [orderA, orderB], primaryBoardColumn: "submitted", tableCodes: ["T36"], aggregateTotal: 60000 })]
        },
        {
            label: "group-CD",
            submitted: [A, B, C, D],
            groups: [
                makeGroup({ id: groupAB, primaryOrderId: orderA, orderIds: [orderA, orderB], primaryBoardColumn: "submitted", tableCodes: ["T36"], aggregateTotal: 60000 }),
                makeGroup({ id: groupCD, primaryOrderId: orderC, orderIds: [orderC, orderD], primaryBoardColumn: "submitted", tableCodes: ["T37", "T38"], aggregateTotal: 90000 })
            ]
        }
    ]);
    multiExpectations.push({
        name: "Multistep D",
        snapshots: multiD.snapshots,
        expected: [
            ["O:" + orderA, "O:" + orderB, "O:" + orderC, "O:" + orderD],
            ["G:" + orderA + "(" + groupAB + ")", "O:" + orderC, "O:" + orderD],
            ["G:" + orderA + "(" + groupAB + ")", "G:" + orderC + "(" + groupCD + ")"]
        ]
    });

    // Scenario E — group A+B, ungroup, then group A+C.
    const multiE = runMultiStepScenario("Multistep E: regroup with different partner", [
        { label: "initial", submitted: [A, B, C], groups: [] },
        {
            label: "group-AB",
            submitted: [A, B, C],
            groups: [makeGroup({ id: groupAB, primaryOrderId: orderA, orderIds: [orderA, orderB], primaryBoardColumn: "submitted", tableCodes: ["T36"], aggregateTotal: 60000 })]
        },
        { label: "ungroup-AB", submitted: [A, B, C], groups: [] },
        {
            label: "group-AC",
            submitted: [A, B, C],
            groups: [makeGroup({ id: groupAC, primaryOrderId: orderA, orderIds: [orderA, orderC], primaryBoardColumn: "submitted", tableCodes: ["T36", "T37"], aggregateTotal: 60000 })]
        }
    ]);
    multiExpectations.push({
        name: "Multistep E",
        snapshots: multiE.snapshots,
        expected: [
            ["O:" + orderA, "O:" + orderB, "O:" + orderC],
            ["G:" + orderA + "(" + groupAB + ")", "O:" + orderC],
            ["O:" + orderA, "O:" + orderB, "O:" + orderC],
            ["G:" + orderA + "(" + groupAC + ")", "O:" + orderB]
        ]
    });

    // Scenario F — group A+B then refresh three times. Each refresh must
    // produce exactly one grouped card; no duplicate, no stale standalone.
    const multiF = runMultiStepScenario("Multistep F: group then refresh x3", [
        {
            label: "group-AB",
            submitted: [A, B],
            groups: [makeGroup({ id: groupAB, primaryOrderId: orderA, orderIds: [orderA, orderB], primaryBoardColumn: "submitted", tableCodes: ["T36"], aggregateTotal: 60000 })]
        },
        {
            label: "refresh-1",
            submitted: [A, B],
            groups: [makeGroup({ id: groupAB, primaryOrderId: orderA, orderIds: [orderA, orderB], primaryBoardColumn: "submitted", tableCodes: ["T36"], aggregateTotal: 60000 })]
        },
        {
            label: "refresh-2",
            submitted: [A, B],
            groups: [makeGroup({ id: groupAB, primaryOrderId: orderA, orderIds: [orderA, orderB], primaryBoardColumn: "submitted", tableCodes: ["T36"], aggregateTotal: 60000 })]
        },
        {
            label: "refresh-3",
            submitted: [A, B],
            groups: [makeGroup({ id: groupAB, primaryOrderId: orderA, orderIds: [orderA, orderB], primaryBoardColumn: "submitted", tableCodes: ["T36"], aggregateTotal: 60000 })]
        }
    ]);
    multiExpectations.push({
        name: "Multistep F",
        snapshots: multiF.snapshots,
        expected: [
            ["G:" + orderA + "(" + groupAB + ")"],
            ["G:" + orderA + "(" + groupAB + ")"],
            ["G:" + orderA + "(" + groupAB + ")"],
            ["G:" + orderA + "(" + groupAB + ")"]
        ]
    });

    let exit = 0;

    // ── Report single-step scenarios ───────────────────────────────────────
    for (let i = 0; i < singleExpectations.length; i++) {
        const exp = singleExpectations[i];
        const got = singleResults[i];
        const ok = exp.groupedCards === got.groupedCards
            && exp.normalCards === got.normalCards
            && exp.submittedCardCount === got.submittedCardCount;
        console.log(`${ok ? "PASS" : "FAIL"} ${exp.name}: grouped=${got.groupedCards} normal=${got.normalCards} submittedTotal=${got.submittedCardCount}`);
        if (!ok) {
            console.log(`       expected: grouped=${exp.groupedCards} normal=${exp.normalCards} submittedTotal=${exp.submittedCardCount}`);
            exit = 1;
        }
    }

    // ── Report multi-step scenarios ────────────────────────────────────────
    for (const exp of multiExpectations) {
        let scenarioOk = true;
        for (let s = 0; s < exp.snapshots.length; s++) {
            const snap = exp.snapshots[s];
            const expected = exp.expected[s];
            const shapeOk = snapshotMatches(snap, expected);
            const dupOk = duplicatesAreEmpty(snap);
            const ok = shapeOk && dupOk;
            if (!ok) scenarioOk = false;
            const tag = `${exp.name} step=${snap.label}`;
            if (ok) {
                console.log(`PASS ${tag}: ${snap.cardDescriptors.filter((d) => d.column === "submitted").length} cards, 0 duplicates`);
            } else {
                console.log(`FAIL ${tag}: ${snap.cardDescriptors.filter((d) => d.column === "submitted").length} cards`);
                const actual = snap.cardDescriptors
                    .filter((d) => d.column === "submitted")
                    .map((d) => `${d.grouped ? "G" : "O"}:${d.orderId || "?"}${d.groupId ? `(${d.groupId})` : ""}`)
                    .sort();
                console.log(`       expected: ${JSON.stringify(expected)}`);
                console.log(`       actual:   ${JSON.stringify(actual)}`);
                if (!dupOk) console.log(`       duplicates: ${JSON.stringify(snap.duplicates)}`);
                exit = 1;
            }
        }
        if (scenarioOk) console.log(`PASS ${exp.name} (all steps)`);
    }

    process.exit(exit);
}

main().catch((err) => {
    console.error("FAIL", err.stack || err.message || err);
    process.exit(1);
});