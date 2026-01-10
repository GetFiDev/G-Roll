import "./firebase"; // Initialize Firebase Admin
import {setGlobalOptions} from "firebase-functions/v2/options";

// Set global options for V2 functions
setGlobalOptions({region: "us-central1", memory: "512MiB"});

// Export all functions from modules
export * from "./modules/achievements.functions";
export * from "./modules/streak.functions";
export * from "./modules/iap.functions";
export * from "./modules/user.functions";
export * from "./modules/session.functions";
export * from "./modules/energy.functions";
export * from "./modules/autopilot.functions";
export * from "./modules/shop.functions";
export * from "./modules/content.functions";
export * from "./modules/leaderboard.functions";
export * from "./modules/scheduler.functions";
export * from "./modules/ad.functions";
export * from "./modules/map.functions";
export * from "./modules/tasks.functions";