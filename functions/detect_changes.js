const fs = require('fs');
const path = require('path');
const { execSync } = require('child_process');

function getChangedFiles() {
    try {
        // Check for both staged and unstaged changes
        const output = execSync('git status --porcelain', { encoding: 'utf-8' });
        return output.split('\n')
            .filter(line => line.trim() !== '')
            .map(line => {
                // Formatting: "M  functions/src/modules/iap.functions.ts"
                // Remove status code (first 2 chars) and trim
                return line.substring(3).trim();
            })
            // Filter only files in functions/src
            .filter(f => f.startsWith('functions/src/') && f.endsWith('.ts'));
    } catch (e) {
        console.error("Error running git status:", e);
        return [];
    }
}

function findExportedFunctions(filePath) {
    if (!fs.existsSync(filePath)) return [];
    const content = fs.readFileSync(filePath, 'utf-8');
    const functions = [];

    // Regex to find "export const functionName = ..." (common for Firebase v2)
    // and "export function functionName..."
    const regexConst = /export\s+const\s+(\w+)\s*=/g;
    const regexFunc = /export\s+function\s+(\w+)/g;

    let match;
    while ((match = regexConst.exec(content)) !== null) {
        functions.push(match[1]);
    }
    while ((match = regexFunc.exec(content)) !== null) {
        functions.push(match[1]);
    }
    return functions;
}

function main() {
    // Run from root usually, but let's handle relative paths
    // Assumes script is run from 'functions' directory or root? 
    // The user runs 'npm run deploy' from 'functions' dir.
    // So CWD is .../G-Roll/functions
    // Git paths are relative to repo root: "functions/src/..."

    const cwd = process.cwd();
    // Verify we are in functions dir
    if (!cwd.endsWith('functions')) {
        console.log("ALL"); // Safety fallback
        return;
    }

    const recursiveFiles = [
        'functions/src/index.ts',
        'functions/src/firebase.ts',
        'functions/package.json',
        'functions/tsconfig.json'
    ];

    const changedFiles = getChangedFiles();

    // If no changes, or critical global files changed, deploy ALL
    if (changedFiles.length === 0) {
        console.log("ALL");
        return;
    }

    for (const f of changedFiles) {
        if (recursiveFiles.includes(f)) {
            console.log("ALL");
            return;
        }
    }

    // Map changed modules to functions
    let allFuncs = [];
    for (const f of changedFiles) {
        // f is like "functions/src/modules/iap.functions.ts"
        // We need to resolve it relative to CWD.
        // CWD is ".../functions". git path includes "functions/".
        // So absolute path is RepoRoot + f.
        // Relative to CWD is "../" + f ? No.
        // RepoRoot is CWD/..

        const absolutePath = path.resolve(cwd, '..', f);

        const funcs = findExportedFunctions(absolutePath);
        if (funcs.length > 0) {
            allFuncs.push(...funcs);
        }
    }

    if (allFuncs.length === 0) {
        console.log("ALL");
    } else {
        // Distinct
        const unique = [...new Set(allFuncs)];
        console.log(unique.join(','));
    }
}

main();
