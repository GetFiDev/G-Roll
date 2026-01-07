#!/bin/bash

# 1. Lint and Fix
echo "------------------------------------------------"
echo "Running: npm run lint -- --fix"
echo "------------------------------------------------"
npm run lint -- --fix
if [ $? -ne 0 ]; then
  echo "❌ Lint failed. Aborting."
  exit 1
fi

# 2. Confirm Build
echo ""
echo "✅ Lint completed (and fixes applied)."
read -p "❓ Proceed to BUILD? (y/n): " confirm_build
if [[ "$confirm_build" != "y" && "$confirm_build" != "Y" ]]; then
  echo "🚫 Aborted by user."
  exit 0
fi

# 3. Build
echo ""
echo "------------------------------------------------"
echo "Running: npm run build"
echo "------------------------------------------------"
npm run build
if [ $? -ne 0 ]; then
  echo "❌ Build failed. Aborting."
  exit 1
fi

# 4. Smart Deploy Check
echo ""
echo "🔍 Checking for changed functions..."
detected=$(node detect_changes.js)

deploy_cmd="firebase deploy --only functions"
target_msg="ALL functions"

if [ "$detected" != "ALL" ] && [ ! -z "$detected" ]; then
    # Format for display: func1, func2
    # Format for command: functions:func1,functions:func2
    
    IFS=',' read -r -a func_array <<< "$detected"
    formatted_cmd=""
    display_list=""
    
    for element in "${func_array[@]}"; do
        if [ -z "$formatted_cmd" ]; then
            formatted_cmd="functions:$element"
            display_list="$element"
        else
            formatted_cmd="$formatted_cmd,functions:$element"
            display_list="$display_list, $element"
        fi
    done
    
    target_msg="ONLY: $display_list"
    deploy_cmd="firebase deploy --only $formatted_cmd"
fi

echo "🎯 Target: $target_msg"
echo ""
echo "Options:"
echo " [y]   Yes, proceed with target"
echo " [a]   Force deploy ALL"
echo " [n]   Cancel"
read -p "Your choice? (y/a/n): " confirm_choice

if [[ "$confirm_choice" == "a" || "$confirm_choice" == "A" ]]; then
    deploy_cmd="firebase deploy --only functions"
    echo "⚠️  Switching to FULL DEPLOY."
elif [[ "$confirm_choice" != "y" && "$confirm_choice" != "Y" ]]; then
    echo "🚫 Aborted by user."
    exit 0
fi

# 5. Deploy
echo ""
echo "------------------------------------------------"
echo "Executing: $deploy_cmd"
echo "------------------------------------------------"
$deploy_cmd
