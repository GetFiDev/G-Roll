import {onCall, HttpsError} from "firebase-functions/v2/https";
import {db} from "../firebase";
import {FieldValue} from "@google-cloud/firestore";

// Helper to sanitize IDs strictly
const sanitizeId = (id: string) => id.replace(/[^a-zA-Z0-9_-]/g, "_");

export const saveMap = onCall(async (req) => {
    // 1. Auth Check (Anonymous allowed?)
    // if (!req.auth) throw new HttpsError("unauthenticated", "User must be logged in.");

    const data = req.data || {};
    const mapName = String(data.mapName || "").trim();
    const mapType = String(data.mapType || "endless").toLowerCase(); // "endless" or "chapter"
    const force = Boolean(data.force);
    const json = String(data.json || "");

    if (!mapName) {
        throw new HttpsError("invalid-argument", "Map Name is required.");
    }

    if (!json) {
        throw new HttpsError("invalid-argument", "Map JSON is empty.");
    }

    // Determine target document based on map type
    // Path structure: appdata(col) -> maps/chapters(doc) -> {mapId}(col) -> raw(doc)
    const mapId = sanitizeId(mapName);
    let targetRef: FirebaseFirestore.DocumentReference;

    if (mapType === "endless") {
        targetRef = db.collection("appdata").doc("maps").collection(mapId).doc("raw");
    } else {
        targetRef = db.collection("appdata").doc("chapters").collection(mapId).doc("raw");
    }

    // 2. Overwrite Check
    if (!force) {
        const snap = await targetRef.get();
        if (snap.exists) {
            return {status: "exists", message: "Map already exists."};
        }
    }

    // 3. Save
    await targetRef.set({
        json: json,
        mapName: mapName,
        mapDisplayName: data.mapDisplayName || mapName,
        mapType: mapType,
        mapOrder: data.mapOrder || 0,
        mapLength: data.mapLength || 100,
        difficultyTag: data.difficultyTag || 1,
        updatedAt: FieldValue.serverTimestamp(),
        // This will overwrite on force, but that's ok/handled by merge if we wanted
        createdAt: FieldValue.serverTimestamp()
    });

    return {status: "success", path: targetRef.path};
});

// ---------------- getChapterMap ----------------
// Returns the chapter map for the user's current progress
// User's progress is stored in users/{uid}/chapterProgress (1-indexed)
export const getChapterMap = onCall(async (req) => {
    const uid = req.auth?.uid;
    if (!uid) {
        throw new HttpsError("unauthenticated", "User must be logged in.");
    }

    const data = req.data || {};
    // Optional: allow explicit chapter override for testing/replay
    let chapterOrder = Number(data.chapterOrder) || 0;

    // If no explicit chapter, get from user's progress
    if (chapterOrder <= 0) {
        const userRef = db.collection("users").doc(uid);
        const userSnap = await userRef.get();

        if (!userSnap.exists) {
            throw new HttpsError("not-found", "User profile not found.");
        }

        const userData = userSnap.data() || {};
        chapterOrder = Number(userData.chapterProgress) || 1;
    }

    // Chapter document ID format: Chapter_{order}
    const chapterId = `Chapter_${chapterOrder}`;

    // Path: appdata(col) -> chapters(doc) -> {chapterId}(col) -> raw(doc)
    const chapterRef = db.collection("appdata").doc("chapters").collection(chapterId).doc("raw");
    const chapterSnap = await chapterRef.get();

    if (!chapterSnap.exists) {
        // Chapter doesn't exist - user completed all chapters!
        return {
            ok: false,
            reason: "no_more_chapters",
            chapterOrder: chapterOrder,
            message: `Chapter ${chapterOrder} does not exist.`
        };
    }

    const chapterData = chapterSnap.data() || {};
    const jsonStr = chapterData.json as string;

    if (!jsonStr || typeof jsonStr !== "string") {
        throw new HttpsError("data-loss", `Chapter ${chapterOrder} has invalid JSON.`);
    }

    console.log(`[getChapterMap] uid=${uid} chapter=${chapterOrder}`);

    return {
        ok: true,
        chapterOrder: chapterOrder,
        mapId: chapterId,
        mapDisplayName: chapterData.mapDisplayName || `Chapter ${chapterOrder}`,
        mapLength: chapterData.mapLength || 100,
        json: jsonStr
    };
});

// ---------------- listMaps ----------------
// Returns all chapters and endless maps for the map browser
export const listMaps = onCall(async () => {
    const chapters: { mapId: string; displayName: string; order: number; createdAt: string }[] = [];
    const endless: { mapId: string; displayName: string; difficulty: number; createdAt: string }[] = [];

    try {
        // List all chapter subcollections under appdata/chapters
        const chaptersDoc = db.collection("appdata").doc("chapters");
        const chapterCollections = await chaptersDoc.listCollections();

        for (const col of chapterCollections) {
            const rawDoc = await col.doc("raw").get();
            if (rawDoc.exists) {
                const data = rawDoc.data() || {};
                chapters.push({
                    mapId: col.id,
                    displayName: (data.mapDisplayName as string) || col.id,
                    order: Number(data.mapOrder) || 0,
                    createdAt: data.createdAt?.toDate?.()?.toISOString?.() || ""
                });
            }
        }

        // List all endless subcollections under appdata/maps
        const mapsDoc = db.collection("appdata").doc("maps");
        const mapCollections = await mapsDoc.listCollections();

        for (const col of mapCollections) {
            const rawDoc = await col.doc("raw").get();
            if (rawDoc.exists) {
                const data = rawDoc.data() || {};
                endless.push({
                    mapId: col.id,
                    displayName: (data.mapDisplayName as string) || col.id,
                    difficulty: Number(data.difficultyTag) || 1,
                    createdAt: data.createdAt?.toDate?.()?.toISOString?.() || ""
                });
            }
        }

        // Sort chapters by order, endless by createdAt desc
        chapters.sort((a, b) => a.order - b.order);
        endless.sort((a, b) => (b.createdAt || "").localeCompare(a.createdAt || ""));

    } catch (e) {
        console.error("[listMaps] Error:", e);
        throw new HttpsError("internal", "Failed to list maps");
    }

    console.log(`[listMaps] chapters=${chapters.length} endless=${endless.length}`);
    return {ok: true, chapters, endless};
});

// ---------------- deleteMap ----------------
// Deletes a map by type and ID
export const deleteMap = onCall(async (req) => {
    const data = req.data || {};
    const mapType = String(data.mapType || "").toLowerCase();
    const mapId = String(data.mapId || "").trim();

    if (!mapType || (mapType !== "endless" && mapType !== "chapter")) {
        throw new HttpsError("invalid-argument", "mapType must be 'endless' or 'chapter'");
    }
    if (!mapId) {
        throw new HttpsError("invalid-argument", "mapId is required");
    }

    let parentDoc: FirebaseFirestore.DocumentReference;
    if (mapType === "endless") {
        parentDoc = db.collection("appdata").doc("maps");
    } else {
        parentDoc = db.collection("appdata").doc("chapters");
    }

    const targetCol = parentDoc.collection(mapId);
    const rawRef = targetCol.doc("raw");

    // Check existence
    const snap = await rawRef.get();
    if (!snap.exists) {
        return {ok: false, message: "Map not found"};
    }

    // Delete the raw doc (and any other docs in the collection)
    const allDocs = await targetCol.listDocuments();
    const batch = db.batch();
    for (const d of allDocs) {
        batch.delete(d);
    }
    await batch.commit();

    // Also remove from pools if endless
    if (mapType === "endless") {
        const poolsCol = db.collection("appdata").doc("maps_meta").collection("pools");
        const poolBatch = db.batch();
        for (const d of [1, 2, 3, 4]) {
            const pRef = poolsCol.doc(String(d));
            poolBatch.update(pRef, {
                ids: FieldValue.arrayRemove(mapId)
            });
        }
        try {
            await poolBatch.commit();
        } catch {
            // Pools might not exist yet, ignore
        }

        // Also delete from by_map
        const byMapRef = db.collection("appdata").doc("maps_meta").collection("by_map").doc(mapId);
        await byMapRef.delete().catch(() => { });
    }

    console.log(`[deleteMap] Deleted ${mapType}/${mapId}`);
    return {ok: true};
});

// ---------------- getMapForEdit ----------------
// Returns full map data for editing (includes JSON)
export const getMapForEdit = onCall(async (req) => {
    const data = req.data || {};
    const mapType = String(data.mapType || "").toLowerCase();
    const mapId = String(data.mapId || "").trim();

    if (!mapType || (mapType !== "endless" && mapType !== "chapter")) {
        throw new HttpsError("invalid-argument", "mapType must be 'endless' or 'chapter'");
    }
    if (!mapId) {
        throw new HttpsError("invalid-argument", "mapId is required");
    }

    let targetRef: FirebaseFirestore.DocumentReference;
    if (mapType === "endless") {
        targetRef = db.collection("appdata").doc("maps").collection(mapId).doc("raw");
    } else {
        targetRef = db.collection("appdata").doc("chapters").collection(mapId).doc("raw");
    }

    const snap = await targetRef.get();
    if (!snap.exists) {
        return {ok: false, message: "Map not found"};
    }

    const mapData = snap.data() || {};

    return {
        ok: true,
        mapId: mapId,
        mapType: mapType,
        mapName: mapData.mapName || mapId,
        mapDisplayName: mapData.mapDisplayName || mapId,
        mapOrder: mapData.mapOrder || 0,
        mapLength: mapData.mapLength || 100,
        difficultyTag: mapData.difficultyTag || 1,
        json: mapData.json || ""
    };
});
