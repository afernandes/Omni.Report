// OmniReport Monaco Editor host.
//
// Provides a thin wrapper around Monaco (the engine that powers VS Code) to give
// the Expression Editor full-fledged syntax highlighting, IntelliSense and bracket
// matching for the OmniReport expression language.
//
// Loads Monaco lazily from CDN on first use. The first call to `mount` blocks
// until Monaco is ready; subsequent calls reuse the loaded module.

(function () {
    "use strict";

    const MONACO_VERSION = "0.45.0";
    const MONACO_BASE = `https://cdn.jsdelivr.net/npm/monaco-editor@${MONACO_VERSION}/min/vs`;

    let loaderPromise = null;
    let languageRegistered = false;
    const editors = new Map(); // id → { editor, dotnetRef }

    function injectGlobalStyles() {
        if (document.getElementById("omni-monaco-styles")) return;
        const style = document.createElement("style");
        style.id = "omni-monaco-styles";
        style.textContent = `
            /* ── Suggest widget (IntelliSense popup) ─────────────────────── *
             * With fixedOverflowWidgets:true Monaco renders the suggest widget
             * directly in <body>, escaping the dialog/modal that hosts the editor.
             * No selector needs to traverse ::deep — these rules apply globally to
             * any Monaco-hosted suggest widget. */
            .monaco-editor .suggest-widget,
            .monaco-editor-overflow-widgets .suggest-widget {
                min-width: 620px !important;
                width: max-content !important;
                max-width: 880px !important;
                font-family: "IBM Plex Mono","Cascadia Code",Consolas,monospace !important;
                border-radius: 6px;
                box-shadow: 0 10px 36px rgba(20,18,14,0.22), 0 2px 6px rgba(20,18,14,0.10) !important;
            }
            /* Row height — descenders (g, p, y) need real vertical room. Use grid layout
               on the row so icon + name + qualifier + description align as columns. */
            .monaco-editor .suggest-widget .monaco-list .monaco-list-row,
            .monaco-editor-overflow-widgets .suggest-widget .monaco-list .monaco-list-row {
                height: 30px !important;
                line-height: 30px !important;
                padding: 0 12px !important;
                display: flex;
                align-items: center;
                box-sizing: border-box;
            }
            .monaco-editor .suggest-widget .monaco-list .monaco-list-row .contents {
                line-height: 30px !important;
                flex: 1;
                min-width: 0;
                overflow: hidden;
            }
            .monaco-editor .suggest-widget .monaco-list .monaco-list-row .main {
                line-height: 30px !important;
                display: flex;
                align-items: center;
                overflow: hidden;
                white-space: nowrap;
            }
            .monaco-editor .suggest-widget .monaco-list .monaco-list-row .label-name {
                font-size: 14px !important;
                line-height: 30px !important;
                font-weight: 500;
                /* Reserve a generous width so namespace.field labels never get truncated. */
                min-width: 240px;
                overflow: visible;
            }
            .monaco-editor .suggest-widget .monaco-list .monaco-list-row .label-description {
                opacity: 0.65;
                font-size: 12px;
                margin-left: 10px;
                line-height: 30px !important;
            }
            .monaco-editor .suggest-widget .monaco-list .monaco-list-row .qualifier-label,
            .monaco-editor .suggest-widget .monaco-list .monaco-list-row .label-detail {
                font-size: 12px;
                opacity: 0.7;
                line-height: 30px !important;
                margin-left: 8px;
            }
            /* Icon column */
            .monaco-editor .suggest-widget .monaco-list .monaco-list-row .codicon,
            .monaco-editor .suggest-widget .monaco-list .monaco-list-row .suggest-icon {
                font-size: 16px !important;
                line-height: 30px !important;
                width: 20px;
                margin-right: 8px;
            }
            /* "Read More" / status bar at the bottom of the widget */
            .monaco-editor .suggest-widget .suggest-status-bar {
                padding: 6px 12px !important;
                font-size: 11.5px !important;
                min-height: 24px;
                line-height: 1.5;
            }

            /* ── Side details panel (auto-shown via showInlineDetails:true) ─ */
            .monaco-editor .suggest-widget .details {
                min-width: 420px !important;
                max-width: 560px !important;
                padding: 12px 14px !important;
                font-size: 13px;
            }
            .monaco-editor .suggest-widget .details .header { font-weight: 600; margin-bottom: 8px; font-size: 13px; }
            .monaco-editor .suggest-widget .details .body { line-height: 1.55; font-size: 12.5px; }
            .monaco-editor .suggest-widget .details .docs { padding: 6px 0; }

            /* ── Hover popup ─────────────────────────────────────────────── */
            .monaco-editor .monaco-hover,
            .monaco-editor-overflow-widgets .monaco-hover {
                min-width: 340px !important;
                max-width: 680px !important;
                border-radius: 6px;
                box-shadow: 0 10px 32px rgba(20,18,14,0.18) !important;
            }
            .monaco-editor .monaco-hover .hover-row .hover-contents {
                padding: 10px 14px;
                line-height: 1.5;
                font-size: 13px;
            }

            /* ── Parameter hints (function signature popup) ──────────────── */
            .monaco-editor .parameter-hints-widget,
            .monaco-editor-overflow-widgets .parameter-hints-widget {
                min-width: 440px !important;
                max-width: 760px !important;
                border-radius: 6px;
                box-shadow: 0 10px 32px rgba(20,18,14,0.18) !important;
            }
            .monaco-editor .parameter-hints-widget .signature {
                padding: 10px 14px;
                line-height: 1.5;
                font-size: 13.5px;
            }
            .monaco-editor .parameter-hints-widget .docs {
                padding: 6px 14px 10px;
                font-size: 12.5px;
            }
        `;
        document.head.appendChild(style);
    }

    function loadMonaco() {
        injectGlobalStyles();
        if (loaderPromise) return loaderPromise;
        loaderPromise = new Promise((resolve, reject) => {
            // Load AMD loader.js once.
            if (window.require && window.require.config) {
                resolve();
                return;
            }
            const script = document.createElement("script");
            script.src = `${MONACO_BASE}/loader.js`;
            script.onload = () => {
                window.require.config({ paths: { vs: MONACO_BASE } });
                // Workaround so monaco's web workers load over HTTP (cross-origin).
                window.MonacoEnvironment = {
                    getWorkerUrl: function () {
                        return `data:text/javascript;charset=utf-8,${encodeURIComponent(`
                            self.MonacoEnvironment = { baseUrl: '${MONACO_BASE}/' };
                            importScripts('${MONACO_BASE}/base/worker/workerMain.js');
                        `)}`;
                    },
                };
                window.require(["vs/editor/editor.main"], () => {
                    registerOmniReportLanguage();
                    resolve();
                });
            };
            script.onerror = () => reject(new Error("Failed to load Monaco from CDN."));
            document.head.appendChild(script);
        });
        return loaderPromise;
    }

    // ─── Custom language: omnireport-expression ───────────────────────────────────
    function registerOmniReportLanguage() {
        if (languageRegistered) return;
        languageRegistered = true;

        monaco.languages.register({ id: "omnireport-expression" });
        monaco.languages.setLanguageConfiguration("omnireport-expression", {
            brackets: [["{", "}"], ["[", "]"], ["(", ")"]],
            autoClosingPairs: [
                { open: "{", close: "}" },
                { open: "[", close: "]" },
                { open: "(", close: ")" },
                { open: '"', close: '"' },
                { open: "'", close: "'" },
            ],
            surroundingPairs: [
                { open: "{", close: "}" },
                { open: "[", close: "]" },
                { open: "(", close: ")" },
                { open: '"', close: '"' },
                { open: "'", close: "'" },
            ],
        });

        monaco.languages.setMonarchTokensProvider("omnireport-expression", {
            defaultToken: "",
            tokenPostfix: ".omni",
            keywords: ["if", "then", "else", "and", "or", "not", "true", "false", "null"],
            aggregates: ["Sum", "Avg", "Count", "Min", "Max", "RunningTotal"],
            functions: ["IIF", "Format", "ToString", "Now", "Today", "PageNumber",
                        "TotalPages", "Upper", "Lower", "Trim", "Substring", "Length",
                        "Replace", "Concat", "Round", "Floor", "Ceiling", "Abs"],
            tokenizer: {
                root: [
                    [/\{/,  { token: "delimiter.brace", next: "@template" }],
                    [/[^{]+/, "text"],
                ],
                template: [
                    [/\}/, { token: "delimiter.brace", next: "@pop" }],
                    [/:/,  { token: "delimiter", next: "@format" }],
                    { include: "@common" },
                ],
                format: [
                    [/[^}]+/, "string.format"],
                    [/\}/, { token: "delimiter.brace", next: "@popall" }],
                ],
                common: [
                    [/\b(Fields|Parameters|Variables|Page|Report)\./, "namespace"],
                    [/\b[A-Z][a-zA-Z0-9_]*\b/, {
                        cases: {
                            "@aggregates": "keyword.aggregate",
                            "@functions": "keyword.function",
                            "@default": "identifier",
                        },
                    }],
                    [/\b[a-z][a-zA-Z0-9_]*\b/, {
                        cases: { "@keywords": "keyword", "@default": "identifier" },
                    }],
                    [/'[^']*'|"[^"]*"/, "string"],
                    [/\d+(\.\d+)?/, "number"],
                    [/[+\-*/%=<>!&|]+/, "operator"],
                    [/[(),]/, "delimiter"],
                    [/\s+/, "white"],
                ],
            },
        });

        // Theme overrides — match the Print Studio palette tokens.
        monaco.editor.defineTheme("omnireport-light", {
            base: "vs",
            inherit: true,
            rules: [
                { token: "namespace",         foreground: "0F766E", fontStyle: "bold" }, // teal
                { token: "keyword.aggregate", foreground: "C2410C", fontStyle: "bold" }, // accent
                { token: "keyword.function",  foreground: "C2410C" },
                { token: "string",            foreground: "047857" },
                { token: "string.format",     foreground: "9333EA" },
                { token: "number",            foreground: "1D4ED8" },
                { token: "delimiter.brace",   foreground: "C2410C", fontStyle: "bold" },
            ],
            colors: {
                "editor.background": "#FFFFFF",
            },
        });

        monaco.editor.defineTheme("omnireport-dark", {
            base: "vs-dark",
            inherit: true,
            rules: [
                { token: "namespace",         foreground: "14B8A6", fontStyle: "bold" },
                { token: "keyword.aggregate", foreground: "F97316", fontStyle: "bold" },
                { token: "keyword.function",  foreground: "F97316" },
                { token: "string",            foreground: "34D399" },
                { token: "string.format",     foreground: "C084FC" },
                { token: "number",            foreground: "93C5FD" },
                { token: "delimiter.brace",   foreground: "F97316", fontStyle: "bold" },
            ],
            colors: {
                "editor.background": "#232323",
            },
        });
    }

    // ─── Completion provider — context-aware Fields/Parameters/functions ──────────
    // Re-registered per editor mount so the field list reflects the current data sources.
    let completionDisposable = null;
    let hoverDisposable = null;
    let _activeItems = [];

    function registerCompletion(items) {
        _activeItems = Array.isArray(items) ? items : [];
        console.log("[monaco] registerCompletion: items=", _activeItems.length);

        if (completionDisposable) { completionDisposable.dispose(); completionDisposable = null; }
        if (hoverDisposable)      { hoverDisposable.dispose();      hoverDisposable = null; }

        completionDisposable = monaco.languages.registerCompletionItemProvider("omnireport-expression", {
            // No trigger characters — only provide on explicit Ctrl+Space and on typing.
            // Returning the COMPLETE list every call is the simplest contract and avoids
            // any "incomplete: true → Monaco re-queries → loading" loops.
            provideCompletionItems: function (model, position) {
                const word = model.getWordUntilPosition(position);
                const range = new monaco.Range(
                    position.lineNumber, word.startColumn,
                    position.lineNumber, word.endColumn);

                const suggestions = [];
                for (let i = 0; i < _activeItems.length; i++) {
                    const it = _activeItems[i];
                    if (!it || !it.label) continue;
                    suggestions.push({
                        label: String(it.label),
                        kind: monacoKind(it.kind),
                        insertText: String(it.insertText || it.label),
                        detail: it.detail ? String(it.detail) : undefined,
                        documentation: it.documentation ? String(it.documentation) : undefined,
                        range: range,
                    });
                }
                console.log("[monaco] provideCompletionItems →", suggestions.length, "items");
                return { suggestions: suggestions };
            },
        });

        hoverDisposable = monaco.languages.registerHoverProvider("omnireport-expression", {
            provideHover: function (model, position) {
                try {
                    const word = model.getWordAtPosition(position);
                    if (!word) return null;
                    const line = model.getLineContent(position.lineNumber);
                    const prefixStart = Math.max(0, word.startColumn - 12);
                    const before = line.substring(prefixStart, word.startColumn - 1);
                    const dotIdx = before.lastIndexOf(".");
                    let label = word.word;
                    if (dotIdx >= 0) {
                        const ns = before.substring(0, dotIdx);
                        if (/^(Fields|Parameters|Variables|Page|Report)$/.test(ns)) {
                            label = ns + "." + word.word;
                        }
                    }
                    const match = _activeItems.find(it =>
                        it.label === label ||
                        it.insertText === label ||
                        (it.label && it.label.startsWith(label + "(")));
                    if (!match) return null;
                    const md = [];
                    if (match.detail) md.push({ value: "**" + match.detail + "**" });
                    md.push({ value: "```\n" + match.label + "\n```" });
                    if (match.documentation) md.push({ value: match.documentation });
                    return {
                        range: new monaco.Range(position.lineNumber, word.startColumn,
                                                position.lineNumber, word.endColumn),
                        contents: md,
                    };
                } catch { return null; }
            },
        });
    }

    function monacoKind(kind) {
        const k = monaco.languages.CompletionItemKind;
        switch ((kind || "").toLowerCase()) {
            case "field":     return k.Field;
            case "parameter": return k.Variable;
            case "function":  return k.Function;
            case "keyword":   return k.Keyword;
            case "snippet":   return k.Snippet;
            default:          return k.Text;
        }
    }

    // ─── Public API ───────────────────────────────────────────────────────────────
    async function mount(opts) {
        const { domId, initialValue, theme, language, completions, dotnetRef } = opts;
        await loadMonaco();

        const host = document.getElementById(domId);
        if (!host) throw new Error(`Monaco mount: #${domId} not in DOM`);

        // Dispose any prior editor on the same element (StateHasChanged re-renders).
        if (editors.has(domId)) {
            editors.get(domId).editor.dispose();
            editors.delete(domId);
        }

        // Conservative editor options. Anything added beyond this baseline was
        // observed to cause Monaco 0.45 to lock the suggest widget in "Loading...".
        const editor = monaco.editor.create(host, {
            value: initialValue || "",
            language: language || "omnireport-expression",
            theme: theme === "dark" ? "omnireport-dark" : "omnireport-light",
            automaticLayout: true,
            minimap: { enabled: false },
            scrollBeyondLastLine: false,
            wordWrap: "on",
            lineNumbers: "off",
            glyphMargin: false,
            folding: false,
            lineDecorationsWidth: 4,
            lineNumbersMinChars: 0,
            renderLineHighlight: "none",
            scrollbar: { vertical: "auto", horizontal: "auto", verticalScrollbarSize: 8, horizontalScrollbarSize: 8 },
            fontFamily: '"IBM Plex Mono", "Cascadia Code", "JetBrains Mono", Consolas, monospace',
            fontSize: 13,
            padding: { top: 8, bottom: 8 },
            tabSize: 2,
            insertSpaces: true,

            // Render suggest/hover popups as fixed-position portals attached to <body>,
            // escaping any parent overflow:hidden container. Same technique VS Code uses.
            fixedOverflowWidgets: true,

            // Row sizing so descenders aren't clipped.
            suggestFontSize: 14,
            suggestLineHeight: 30,

            // Word-based suggestions OFF: only OUR provider answers. Without this,
            // Monaco's built-in word collector runs in the worker and frequently is
            // the actual culprit behind a stuck "Loading..." spinner.
            wordBasedSuggestions: false,
        });

        // Always (re)register so the language has a fresh provider per editor mount —
        // even with zero items, this prevents Monaco from waiting forever on a stale
        // disposed provider and showing a permanent "Loading…" placeholder.
        registerCompletion(Array.isArray(completions) ? completions : []);

        editor.onDidChangeModelContent(() => {
            const value = editor.getValue();
            if (dotnetRef) {
                dotnetRef.invokeMethodAsync("OnEditorValueChanged", value)
                    .catch(err => console.warn("[monaco] dotnet callback failed", err));
            }
        });

        editor.focus();
        editors.set(domId, { editor, dotnetRef });
    }

    function unmount(domId) {
        const ent = editors.get(domId);
        if (ent) {
            ent.editor.dispose();
            editors.delete(domId);
        }
    }

    function getValue(domId) {
        const ent = editors.get(domId);
        return ent ? ent.editor.getValue() : null;
    }

    function setValue(domId, value) {
        const ent = editors.get(domId);
        if (ent) ent.editor.setValue(value || "");
    }

    function insertAtCursor(domId, text) {
        const ent = editors.get(domId);
        if (!ent) return;
        const selection = ent.editor.getSelection();
        ent.editor.executeEdits("insert", [{
            range: selection,
            text: text,
            forceMoveMarkers: true,
        }]);
        ent.editor.focus();
    }

    function setTheme(theme) {
        if (!window.monaco) return;
        monaco.editor.setTheme(theme === "dark" ? "omnireport-dark" : "omnireport-light");
    }

    window.omniMonaco = { mount, unmount, getValue, setValue, insertAtCursor, setTheme };
})();
