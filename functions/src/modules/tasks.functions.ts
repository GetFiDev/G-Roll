import {onCall, HttpsError} from "firebase-functions/v2/https";
import {db} from "../firebase";
import {FieldValue, Timestamp} from "@google-cloud/firestore";

// ========================= Types =========================

interface TaskDefinition {
    taskId: string;
    taskDisplayName: string;
    taskDisplayDescription: string;
    taskCurrencyReward: number;
    taskDirectionUrl: string;
    taskIconUrl: string;
}

// ========================= Helpers =========================

const userTaskCompletionRef = (uid: string, taskId: string) =>
    db.collection("users").doc(uid).collection("taskCompletion").doc(taskId);

// ========================= Exports =========================

/**
 * getAvailableTasks
 * Fetches all tasks from appdata/taskDatas and filters out completed ones for the user.
 * Returns only uncompleted tasks.
 */
export const getAvailableTasks = onCall(async (req) => {
    const uid = req.auth?.uid;
    if (!uid) throw new HttpsError("unauthenticated", "Auth required.");

    try {
        // 1) Get all task IDs from appdata/taskDatas collection
        const taskDatasCol = db.collection("appdata").doc("taskDatas").collection("tasks");
        const taskSnap = await taskDatasCol.get();

        if (taskSnap.empty) {
            return {ok: true, tasks: []};
        }

        // 2) Get user's completed tasks
        const completionCol = db.collection("users").doc(uid).collection("taskCompletion");
        const completionSnap = await completionCol.get();

        const completedTaskIds = new Set<string>();
        for (const doc of completionSnap.docs) {
            const isCompleted = doc.get("isCompleted");
            if (isCompleted === true) {
                completedTaskIds.add(doc.id);
            }
        }

        // 3) Build available tasks list
        const tasks: TaskDefinition[] = [];

        for (const doc of taskSnap.docs) {
            const taskId = doc.id;

            // Skip if already completed
            if (completedTaskIds.has(taskId)) {
                continue;
            }

            const data = doc.data() || {};
            tasks.push({
                taskId,
                taskDisplayName: typeof data.taskDisplayName === "string" ? data.taskDisplayName : "",
                taskDisplayDescription: typeof data.taskDisplayDescription === "string" ? data.taskDisplayDescription : "",
                taskCurrencyReward: Number(data.taskCurrencyReward ?? 0.5) || 0.5,
                taskDirectionUrl: typeof data.taskDirectionUrl === "string" ? data.taskDirectionUrl : "",
                taskIconUrl: typeof data.taskIconUrl === "string" ? data.taskIconUrl : "",
            });
        }

        return {ok: true, tasks};
    } catch (error: any) {
        console.error("[getAvailableTasks] Error:", error);
        throw new HttpsError("internal", error.message || "Failed to fetch tasks");
    }
});

/**
 * completeTask
 * Marks a task as completed and grants the reward to user's currency.
 * Idempotent: if already completed, returns success without re-granting reward.
 */
export const completeTask = onCall(async (req) => {
    const uid = req.auth?.uid;
    if (!uid) throw new HttpsError("unauthenticated", "Auth required.");

    const taskId = String(req.data?.taskId || "").trim();
    if (!taskId) throw new HttpsError("invalid-argument", "taskId is required");

    const now = Timestamp.now();
    const userRef = db.collection("users").doc(uid);
    const completionRef = userTaskCompletionRef(uid, taskId);

    // Read task definition to get reward amount
    const taskDatasCol = db.collection("appdata").doc("taskDatas").collection("tasks");
    const taskDefSnap = await taskDatasCol.doc(taskId).get();

    if (!taskDefSnap.exists) {
        throw new HttpsError("not-found", `Task definition not found: ${taskId}`);
    }

    const taskData = taskDefSnap.data() || {};
    const reward = Number(taskData.taskCurrencyReward ?? 0.5) || 0.5;

    // Transaction: check completion status, grant reward if not completed
    const result = await db.runTransaction(async (tx) => {
        const [userSnap, completionSnap] = await Promise.all([
            tx.get(userRef),
            tx.get(completionRef),
        ]);

        if (!userSnap.exists) {
            throw new HttpsError("failed-precondition", "User document missing");
        }

        // Check if already completed
        if (completionSnap.exists && completionSnap.get("isCompleted") === true) {
            // Already completed - return current state without re-rewarding
            const currentCurrency = Number(userSnap.get("currency") ?? 0) || 0;
            return {
                alreadyCompleted: true,
                rewardGranted: 0,
                newCurrency: currentCurrency,
            };
        }

        // Grant reward
        const currentCurrency = Number(userSnap.get("currency") ?? 0) || 0;
        const newCurrency = currentCurrency + reward;

        // Update user currency
        tx.set(userRef, {
            currency: newCurrency,
            updatedAt: FieldValue.serverTimestamp(),
        }, {merge: true});

        // Mark task as completed
        tx.set(completionRef, {
            isCompleted: true,
            latestEditTime: now,
            rewardGranted: reward,
        });

        return {
            alreadyCompleted: false,
            rewardGranted: reward,
            newCurrency,
        };
    });

    return {
        ok: true,
        taskId,
        ...result,
    };
});
