const instances = {};

function ensureJodit() {
    if (window.Jodit) return Promise.resolve();
    return new Promise((resolve, reject) => {
        const existing = document.querySelector('script[src*="jodit.min.js"]');
        if (existing) {
            const poll = setInterval(() => {
                if (window.Jodit) { clearInterval(poll); resolve(); }
            }, 30);
            setTimeout(() => { clearInterval(poll); reject(new Error('Jodit load timeout')); }, 15000);
        } else {
            const s = document.createElement('script');
            s.src = 'https://cdn.jsdelivr.net/npm/jodit@3.24.6/build/jodit.min.js';
            s.onload = resolve;
            s.onerror = () => reject(new Error('Jodit CDN failed'));
            document.head.appendChild(s);
        }
    });
}

// Copy @font-face rules into Jodit's iframe document if it uses one
function copyFontFacesToIframe(editorDoc) {
    if (!editorDoc || editorDoc === document) return;
    document.querySelectorAll('style').forEach(style => {
        if (!style.textContent.includes('@font-face')) return;
        const copyId = (style.id || 'anon') + '--iframe-copy';
        if (editorDoc.getElementById(copyId)) return;
        const copy = editorDoc.createElement('style');
        copy.id = copyId;
        copy.textContent = style.textContent;
        (editorDoc.head || editorDoc.documentElement).appendChild(copy);
    });
}

export async function init(id, config) {
    await ensureJodit();
    if (instances[id]) instances[id].destruct();

    instances[id] = Jodit.make('#' + id, config);

    // Propagate @font-face rules to the editor iframe (if Jodit uses one)
    const ed = instances[id];
    const applyFonts = () => copyFontFacesToIframe(ed.editorDocument);
    if (ed.isReady) {
        applyFonts();
    } else {
        ed.events.on('afterInit', applyFonts);
    }
}

export function getContent(id) {
    return instances[id]?.value ?? '';
}

export function setContent(id, html) {
    const ed = instances[id];
    if (ed) ed.value = html;
}

export function insertAtCursor(id, text) {
    const ed = instances[id];
    if (ed) ed.selection.insertHTML(text);
}

export function destroy(id) {
    if (instances[id]) {
        instances[id].destruct();
        delete instances[id];
    }
}
