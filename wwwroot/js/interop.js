// Auth interop
window.authInterop = {
    login: async (email, password, rememberMe) => {
        try {
            const resp = await fetch('/api/auth/login', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ email, password, rememberMe })
            });
            if (resp.ok) return 'ok';
            const text = await resp.text();
            return text ? text.replace(/^"|"$/g, '') : 'Error al iniciar sesión.';
        } catch {
            return 'Error de conexión.';
        }
    },
    logout: async () => {
        await fetch('/api/auth/logout', { method: 'POST' });
    }
};

// File download interop
window.downloadFile = (fileName, contentType, bytes) => {
    const blob = new Blob([new Uint8Array(bytes)], { type: contentType });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
};

// Rich text editor interop
window.editorInterop = {
    init: (id) => {
        const el = document.getElementById(id);
        if (el) el.focus();
    },
    exec: (cmd, value) => {
        document.execCommand(cmd, false, value || null);
    },
    getContent: (id) => {
        const el = document.getElementById(id);
        return el ? el.innerHTML : '';
    },
    setContent: (id, html) => {
        const el = document.getElementById(id);
        if (el) el.innerHTML = html;
    },
    insertAtCursor: (text) => {
        document.execCommand('insertText', false, text);
    }
};

window.joditInterop = {
    _instances: {},

    init: (id, config) => {
        if (joditInterop._instances[id]) {
            joditInterop._instances[id].destruct();
        }
        joditInterop._instances[id] = Jodit.make('#' + id, config);
    },

    getContent: (id) => joditInterop._instances[id]?.value ?? '',

    setContent: (id, html) => {
        const ed = joditInterop._instances[id];
        if (ed) ed.value = html;
    },

    insertAtCursor: (id, text) => {
        const ed = joditInterop._instances[id];
        if (ed) ed.selection.insertHTML(text);
    },

    destroy: (id) => {
        if (joditInterop._instances[id]) {
            joditInterop._instances[id].destruct();
            delete joditInterop._instances[id];
        }
    }
};
