const DB_NAME = "sprout";
const DB_VERSION = 4;
let dbPromise = null;

function showBlockedNotice() {
    if (document.getElementById("sprout-blocked")) {
        return;
    }

    const notice = document.createElement("div");
    notice.id = "sprout-blocked";
    notice.setAttribute("role", "alert");
    notice.textContent = "Sprout is open in another tab with an older version. Close or reload the other Sprout tabs and this one will finish loading.";
    notice.style.cssText = "position:fixed;left:16px;right:16px;bottom:16px;z-index:1000;padding:14px 18px;" +
        "border-radius:12px;background:#1B2A2F;color:#FFFFFF;font:15px/1.4 system-ui,sans-serif;text-align:center";
    document.body.appendChild(notice);
}

function hideBlockedNotice() {
    document.getElementById("sprout-blocked")?.remove();
}

function openDb() {
    if (!dbPromise) {
        dbPromise = new Promise((resolve, reject) => {
            const request = indexedDB.open(DB_NAME, DB_VERSION);

            // Runs the first time, and whenever DB_VERSION goes up
            request.onupgradeneeded = () => {
                const db = request.result;
                if (!db.objectStoreNames.contains("tasks")) {
                    db.createObjectStore("tasks", { keyPath: "id" });
                }
                if (!db.objectStoreNames.contains("settings")) {
                    db.createObjectStore("settings", { keyPath: "id" });
                }
                if (!db.objectStoreNames.contains("handles")) {
                    db.createObjectStore("handles");
                }
                for (const name of ["projects", "meetings", "reports"]) {
                    if (!db.objectStoreNames.contains(name)) {
                        db.createObjectStore(name, { keyPath: "id" });
                    }
                }
            };

            // An older copy of Sprout open in another tab is holding the database. Opening
            // waits until that tab lets go, so say so instead of sitting on the loading screen.
            request.onblocked = () => showBlockedNotice();

            request.onsuccess = () => {
                const db = request.result;
                hideBlockedNotice();

                // When a newer Sprout opens in another tab, let go so it can upgrade.
                db.onversionchange = () => {
                    db.close();
                    dbPromise = null;
                };

                resolve(db);
            };
            request.onerror = () => reject(request.error);
        });
    }
    return dbPromise;
}

export async function getAll(storeName) {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const request = db.transaction(storeName, "readonly")
            .objectStore(storeName)
            .getAll();
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
    });
}

export async function put(storeName, item) {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const tx = db.transaction(storeName, "readwrite");
        tx.objectStore(storeName).put(item);
        tx.oncomplete = () => resolve();
        tx.onerror = () => reject(tx.error);
    });
}

export async function requestPersist() {
    if (navigator.storage && navigator.storage.persist) {
        return await navigator.storage.persist();
    }
    return false;
}

export async function get(storeName, key) {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const request = db.transaction(storeName, "readonly")
            .objectStore(storeName)
            .get(key);
        request.onsuccess = () => resolve(request.result ?? null);
        request.onerror = () => reject(request.error);
    });
}

export async function replaceAll(storeName, items) {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const tx = db.transaction(storeName, "readwrite");
        const store = tx.objectStore(storeName);
        store.clear();
        for (const item of items) {
            store.put(item);
        }
        tx.oncomplete = () => resolve();
        tx.onerror = () => reject(tx.error);
    });
}

export function downloadText(fileName, text) {
    const blob = new Blob([text], { type: "application/json" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(url);
}

async function getFolderHandle() {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const request = db.transaction("handles", "readonly")
            .objectStore("handles")
            .get("backupFolder");

        request.onsuccess = () => resolve(request.result ?? null);
        request.onerror = () => reject(request.error);
    });
}

async function saveFolderHandle(handle) {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const tx = db.transaction("handles", "readwrite");
        tx.objectStore("handles").put(handle, "backupFolder");
        tx.oncomplete = () => resolve();
        tx.onerror = () => reject(tx.error);
    });
}

export function isFolderBackupSupported() {
    return "showDirectoryPicker" in window;
}

export async function chooseBackupFolder() {
    try {
        const handle = await window.showDirectoryPicker({
            id: "sprout-backups",
            mode: "readwrite",
            startIn: "documents"
        });
        await saveFolderHandle(handle);
        return handle.name;
    } catch (error) {
        if (error.name === "AbortError") {
            return null;   // the picker was closed without choosing
        }
        throw error;
    }
}

export async function writeBackupFile(fileName, text, allowPrompt) {
    const handle = await getFolderHandle();
    if (!handle) {
        return "no-folder";
    }

    let permission = await handle.queryPermission({ mode: "readwrite" });
    if (permission !== "granted" && allowPrompt) {
        permission = await handle.requestPermission({ mode: "readwrite" });
    }
    if (permission !== "granted") {
        return "permission";
    }

    try {
        const file = await handle.getFileHandle(fileName, { create: true });
        const writable = await file.createWritable();
        await writable.write(text);
        await writable.close();
        return "ok";
    } catch {
        return "error";
    }
}

export async function remove(storeName, key) {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const tx = db.transaction(storeName, "readwrite");
        tx.objectStore(storeName).delete(key);
        tx.oncomplete = () => resolve();
        tx.onerror = () => reject(tx.error);
    });
}

export async function putMany(storeName, items) {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const tx = db.transaction(storeName, "readwrite");
        const store = tx.objectStore(storeName);
        for (const item of items) {
            store.put(item);
        }
        tx.oncomplete = () => resolve();
        tx.onerror = () => reject(tx.error);
    });
}

// ---- Weekly report ----

// Copies the report as formatted tables (HTML) with a plain-text fallback.
export async function copyReport(html, text) {
    try {
        if (window.ClipboardItem && navigator.clipboard && navigator.clipboard.write) {
            await navigator.clipboard.write([new ClipboardItem({
                "text/html": new Blob([html], { type: "text/html" }),
                "text/plain": new Blob([text], { type: "text/plain" })
            })]);
            return "html";
        }
    } catch {
        // fall through to plain text
    }

    try {
        await navigator.clipboard.writeText(text);
        return "text";
    } catch {
        return "blocked";
    }
}

async function getHandle(key) {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const request = db.transaction("handles", "readonly").objectStore("handles").get(key);
        request.onsuccess = () => resolve(request.result ?? null);
        request.onerror = () => reject(request.error);
    });
}

async function saveHandle(key, handle) {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const tx = db.transaction("handles", "readwrite");
        tx.objectStore("handles").put(handle, key);
        tx.oncomplete = () => resolve();
        tx.onerror = () => reject(tx.error);
    });
}

export function isFilePickerSupported() {
    return "showOpenFilePicker" in window;
}

// Lets Peter pick his report workbook once; Sprout remembers it for next week.
export async function chooseReportWorkbook() {
    try {
        const [handle] = await window.showOpenFilePicker({
            id: "sprout-report",
            startIn: "documents",
            types: [{
                description: "Excel workbook",
                accept: { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet": [".xlsx", ".xlsm"] }
            }]
        });
        await saveHandle("reportWorkbook", handle);
        return handle.name;
    } catch (error) {
        if (error.name === "AbortError") {
            return null;
        }
        throw error;
    }
}

// "ok", "no-file" or "permission".
export async function reportWorkbookStatus() {
    const handle = await getHandle("reportWorkbook");
    if (!handle) {
        return "no-file";
    }

    let permission = await handle.queryPermission({ mode: "readwrite" });
    if (permission !== "granted") {
        permission = await handle.requestPermission({ mode: "readwrite" });
    }
    return permission === "granted" ? "ok" : "permission";
}

export async function readReportWorkbook() {
    const handle = await getHandle("reportWorkbook");
    const file = await handle.getFile();
    return new Uint8Array(await file.arrayBuffer());
}

// "ok", "locked" (usually open in Excel) or "missing".
export async function writeReportWorkbook(bytes) {
    const handle = await getHandle("reportWorkbook");
    try {
        const writable = await handle.createWritable();
        await writable.write(bytes);
        await writable.close();
        return "ok";
    } catch (error) {
        return error.name === "NotFoundError" ? "missing" : "locked";
    }
}

export function downloadBytes(fileName, bytes) {
    const blob = new Blob([bytes], { type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(url);
}
