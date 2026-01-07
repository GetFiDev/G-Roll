"use strict";
var __createBinding = (this && this.__createBinding) || (Object.create ? (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    var desc = Object.getOwnPropertyDescriptor(m, k);
    if (!desc || ("get" in desc ? !m.__esModule : desc.writable || desc.configurable)) {
      desc = { enumerable: true, get: function() { return m[k]; } };
    }
    Object.defineProperty(o, k2, desc);
}) : (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    o[k2] = m[k];
}));
var __exportStar = (this && this.__exportStar) || function(m, exports) {
    for (var p in m) if (p !== "default" && !Object.prototype.hasOwnProperty.call(exports, p)) __createBinding(exports, m, p);
};
Object.defineProperty(exports, "__esModule", { value: true });
require("./firebase"); // Initialize Firebase Admin
const options_1 = require("firebase-functions/v2/options");
// Set global options for V2 functions
(0, options_1.setGlobalOptions)({ region: "us-central1", memory: "512MiB" });
// Export all functions from modules
__exportStar(require("./modules/achievements.functions"), exports);
__exportStar(require("./modules/streak.functions"), exports);
__exportStar(require("./modules/iap.functions"), exports);
__exportStar(require("./modules/user.functions"), exports);
__exportStar(require("./modules/session.functions"), exports);
__exportStar(require("./modules/energy.functions"), exports);
__exportStar(require("./modules/autopilot.functions"), exports);
__exportStar(require("./modules/shop.functions"), exports);
__exportStar(require("./modules/content.functions"), exports);
__exportStar(require("./modules/leaderboard.functions"), exports);
__exportStar(require("./modules/scheduler.functions"), exports);
__exportStar(require("./modules/ad.functions"), exports);
//# sourceMappingURL=index.js.map