import * as admin from "firebase-admin";
import {Firestore} from "@google-cloud/firestore";
import {DB_ID} from "./utils/constants";

console.log("[STARTUP] Loading firebase config...");
try {
    admin.initializeApp();
    console.log("[STARTUP] Firebase Admin initialized");
} catch (e) {
    console.error("[STARTUP] Firebase Admin init failed", e);
}

const PROJECT_ID = process.env.GCLOUD_PROJECT || process.env.GCLOUD_PROJECT_ID || "";
const DB_PATH = `projects/${PROJECT_ID}/databases/${DB_ID}`;
console.log(`[boot] Firestore DB selected: ${DB_PATH}`);

export const db = new Firestore({databaseId: DB_ID});
export {admin};
