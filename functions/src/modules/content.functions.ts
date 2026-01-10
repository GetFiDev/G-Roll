import {onCall, HttpsError} from "firebase-functions/v2/https";
import {onDocumentWritten} from "firebase-functions/v2/firestore";
import {db} from "../firebase";
import {FieldValue, Timestamp} from "@google-cloud/firestore";
import {DB_ID} from "../utils/constants";

// ---------------- getGalleryItems ----------------
export const getGalleryItems = onCall(async (req) => {
    const collectionPath =
        (req.data?.collectionPath as string) ||
        "appdata/galleryitems/itemdata";

    let ids: string[] = Array.isArray(req.data?.ids)
        ? req.data.ids.slice(0, 10).map((x: any) => String(x))
        : ["galleryview_1", "galleryview_2", "galleryview_3"];

    if (!collectionPath.startsWith("appdata/")) {
        throw new HttpsError("permission-denied", "Invalid collectionPath");
    }

    const pickString = (obj: Record<string, any>, keys: string[]): string => {
        for (const k of keys) {
            const v = obj?.[k];
            if (typeof v === "string" && v.trim().length > 0) return v;
        }
        return "";
    };

    try {
        const col = db.collection(collectionPath);
        const snaps = await Promise.all(ids.map((id) => col.doc(id).get()));

        const items = snaps
            .filter((s) => s.exists)
            .map((s) => {
                const d = s.data() || {};

                // Çoklu isim varyasyonları:
                const pngUrl = pickString(d, [
                    "png_url",
                    "pngUrl",
                    "image_url",
                    "imageUrl",
                    "url",
                ]);

                const descriptionText = pickString(d, [
                    "description_text",
                    "descriptionText",
                    "desc",
                    "text",
                ]);

                const guidanceKey = pickString(d, [
                    "guidance_string_key",
                    "guidanceKey",
                    "guidance",
                    "action",
                ]);

                // Sunucu logu (debug)
                console.log(
                    `[getGalleryItems] ${s.id} pngUrl='${pngUrl}' guidance='${guidanceKey}' descLen=${descriptionText.length}`
                );

                return {
                    id: s.id,
                    pngUrl,
                    descriptionText,
                    guidanceKey,
                };
            });

        return {ok: true, items};

    } catch (e) {
        console.error("getGalleryItems error:", e);
        throw new HttpsError("internal", "Failed to fetch gallery items.");
    }
});

// ---------------- indexMapMeta ----------------
export const indexMapMeta = onDocumentWritten(
    {document: "appdata/maps/{mapId}/raw", database: DB_ID},
    async (event) => {
        const mapId = event.params.mapId as string;

        // If the raw doc was deleted -> remove from pools and meta
        if (!event.data?.after?.exists) {
            const metaDoc = db.collection("appdata").doc("maps_meta");
            const byMapRef = metaDoc.collection("by_map").doc(mapId);
            const poolsCol = metaDoc.collection("pools");

            await db.runTransaction(async (tx) => {
                tx.delete(byMapRef);
                for (const d of [1, 2, 3, 4]) {
                    const pRef = poolsCol.doc(String(d));
                    tx.set(
                        pRef,
                        {
                            ids: FieldValue.arrayRemove(mapId),
                            updatedAt: FieldValue.serverTimestamp(),
                        },
                        {merge: true}
                    );
                }
            });

            console.log(`[indexMapMeta] raw deleted: ${mapId}`);
            return;
        }

        // Created/Updated: parse difficultyTag from the single string field
        const after = event.data.after.data() || {};
        let jsonStr = "";
        if (typeof after.json === "string") {
            jsonStr = after.json;
        } else {
            const keys = Object.keys(after);
            if (keys.length === 1 && typeof after[keys[0]] === "string") {
                jsonStr = after[keys[0]];
            }
        }

        if (!jsonStr) {
            console.warn(`[indexMapMeta] '${mapId}' has no single string field to parse`);
            return;
        }

        let difficultyTag = 0;
        try {
            const parsed = JSON.parse(jsonStr);
            const d = Number(parsed?.difficultyTag ?? 0);
            if ([1, 2, 3, 4].includes(d)) difficultyTag = d;
        } catch (e) {
            console.error(`[indexMapMeta] JSON parse error for '${mapId}':`, e);
            return;
        }

        if (!difficultyTag) {
            console.warn(`[indexMapMeta] '${mapId}' missing/invalid difficultyTag`);
            return;
        }

        const metaDoc = db.collection("appdata").doc("maps_meta");
        const byMapRef = metaDoc.collection("by_map").doc(mapId);
        const poolsCol = metaDoc.collection("pools");
        const now = Timestamp.now();

        await db.runTransaction(async (tx) => {
            // Upsert by_map/{mapId}
            const prev = await tx.get(byMapRef);
            const prevDiff = prev.exists
                ? Number(prev.get("difficultyTag") ?? 0)
                : 0;

            tx.set(
                byMapRef,
                {
                    difficultyTag,
                    updatedAt: FieldValue.serverTimestamp(),
                    ...(prev.exists ? {} : {createdAt: now}),
                },
                {merge: true}
            );

            // Maintain pools/{1|2|3|4}.ids arrays
            for (const d of [1, 2, 3, 4]) {
                const pRef = poolsCol.doc(String(d));
                if (d === difficultyTag) {
                    tx.set(
                        pRef,
                        {
                            ids: FieldValue.arrayUnion(mapId),
                            updatedAt: FieldValue.serverTimestamp(),
                        },
                        {merge: true}
                    );
                } else if (prevDiff && d === prevDiff) {
                    tx.set(
                        pRef,
                        {
                            ids: FieldValue.arrayRemove(mapId),
                            updatedAt: FieldValue.serverTimestamp(),
                        },
                        {merge: true}
                    );
                } else {
                    // no-op
                }
            }
        });

        console.log(
            `[indexMapMeta] indexed '${mapId}' diff=${difficultyTag}`
        );
    }
);

// ---------------- getSequencedMaps ----------------
export const getSequencedMaps = onCall(async (req) => {
    const uid = req.auth?.uid;
    if (!uid) throw new HttpsError("unauthenticated", "Auth required.");

    const clamp = (n: number, a: number, b: number) => Math.max(a, Math.min(b, n));
    const count = clamp(Number(req.data?.count ?? 24), 1, 50);
    const seedIn = String(req.data?.seed ?? "");

    // tiny deterministic RNG (xorshift32-like) from a seed string
    const makeRng = (s: string) => {
        let h = 2166136261 >>> 0;
        for (let i = 0; i < s.length; i++) {
            h ^= s.charCodeAt(i);
            h = Math.imul(h, 16777619) >>> 0;
        }
        return () => {
            h ^= h << 13; h >>>= 0;
            h ^= h >>> 17; h >>>= 0;
            h ^= h << 5; h >>>= 0;
            return (h >>> 0) / 4294967296;
        };
    };
    const rng = seedIn ? makeRng(seedIn) : Math.random;

    // 1) read recurring difficulty curve
    const curveRef = db.collection("appdata").doc("recurring_difficulty_curve");
    const curveSnap = await curveRef.get();
    const curve = curveSnap.exists ? curveSnap.data() ?? {} : {};
    const veryEasy = clamp(Number(curve.veryEasy ?? 2), 0, 24);
    const easy = clamp(Number(curve.easy ?? 3), 0, 24);
    const medium = clamp(Number(curve.medium ?? 2), 0, 24);
    const hard = clamp(Number(curve.hard ?? 1), 0, 24);

    const cycle: number[] = [
        ...Array(veryEasy).fill(1), // VeryEasy = 1
        ...Array(easy).fill(2),     // Easy = 2
        ...Array(medium).fill(3),   // Medium = 3
        ...Array(hard).fill(4),     // Hard = 4
    ];
    if (cycle.length === 0) {
        throw new HttpsError("failed-precondition", "Empty pattern");
    }

    const pattern: number[] = [];
    while (pattern.length < count) {
        for (const d of cycle) {
            if (pattern.length >= count) break;
            pattern.push(d);
        }
    }

    // 2) load pools
    const poolsCol = db.collection("appdata")
        .doc("maps_meta").collection("pools");

    const [p1, p2, p3, p4] = await Promise.all(
        [1, 2, 3, 4].map(async (d) => {
            const s = await poolsCol.doc(String(d)).get();
            const ids = (s.exists ? (s.get("ids") as string[] | undefined) : []) || [];
            // copy and shallow-shuffle for variety
            const arr = ids.slice();
            for (let i = arr.length - 1; i > 0; i--) {
                const j = Math.floor((rng() || Math.random()) * (i + 1));
                const t = arr[i]; arr[i] = arr[j]; arr[j] = t;
            }
            return {d, ids, bag: arr};
        })
    );

    const poolByDiff: Record<number, { d: number; ids: string[]; bag: string[] }> = {
        1: p1, 2: p2, 3: p3, 4: p4
    };

    // helper to take one id from pool; allows repeats if exhausted
    const takeFrom = (d: number): string => {
        const p = poolByDiff[d];
        if (!p) throw new HttpsError("not-found", `Pool ${d} missing`);
        if (p.bag.length === 0) {
            if (p.ids.length === 0) {
                throw new HttpsError("not-found", `Pool ${d} is empty`);
            }
            // refill (allow repeats after exhaustion)
            p.bag = p.ids.slice();
        }
        return p.bag.pop() as string;
    };

    // 3) pick ids by pattern
    const chosen: { mapId: string; difficultyTag: number }[] = [];
    for (const d of pattern) {
        const id = takeFrom(d);
        chosen.push({mapId: id, difficultyTag: d});
    }

    // 4) fetch raw jsons in parallel (dedup reads)
    const uniqueIds = Array.from(new Set(chosen.map(x => x.mapId)));
    const rawCol = db.collection("appdata").doc("maps");
    const snaps = await Promise.all(
        uniqueIds.map(id => rawCol.collection(id).doc("raw").get())
    );
    const byId: Record<string, string> = {};
    for (let i = 0; i < uniqueIds.length; i++) {
        const id = uniqueIds[i];
        const s = snaps[i];
        if (!s.exists) {
            throw new HttpsError("not-found", `raw missing: ${id}`);
        }
        const d = s.data() ?? {};
        let jsonStr = "";
        if (typeof (d as any).json === "string") {
            jsonStr = (d as any).json as string;
        } else {
            const ks = Object.keys(d);
            if (ks.length === 1 && typeof (d as any)[ks[0]] === "string") {
                jsonStr = (d as any)[ks[0]] as string;
            }
        }
        if (typeof jsonStr !== "string" || jsonStr.length === 0) {
            throw new HttpsError("data-loss", `raw.json not string for ${id}`);
        }
        byId[id] = jsonStr;
    }

    // 5) assemble entries
    const entries = chosen.map(x => {
        const js = byId[x.mapId];
        if (typeof js !== "string") {
            throw new HttpsError("data-loss", `json not string for ${x.mapId}`);
        }
        return {mapId: x.mapId, difficultyTag: x.difficultyTag, json: js};
    });

    console.log(
        `[getSequencedMaps] uid=${uid} count=${count} pat=${pattern.join("")}`
    );

    return {
        ok: true,
        count,
        pattern,
        entries
    };
});
