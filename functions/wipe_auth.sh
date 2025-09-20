#!/usr/bin/env bash
set -euo pipefail

PROJECT_ID="getfi-7f987"
ACCESS_TOKEN="$(gcloud auth print-access-token)"
BASE="https://identitytoolkit.googleapis.com/v1"

NEXT=""
TOTAL=0

while true; do
  URL="${BASE}/accounts:batchGet?maxResults=1000&targetProjectId=${PROJECT_ID}"
  if [ -n "$NEXT" ]; then
    URL="${URL}&nextPageToken=${NEXT}"
  fi

  RESP="$(curl -s -H "Authorization: Bearer ${ACCESS_TOKEN}" "$URL")"

  # Kullanıcı ID'lerini topla
  IDS="$(echo "$RESP" | jq -r '.users[]?.localId' || true)"
  NEXT="$(echo "$RESP" | jq -r '.nextPageToken // empty' || true)"

  # Hiç user yok ve sayfa da yoksa bitir
  if [ -z "$IDS" ] && [ -z "$NEXT" ]; then
    break
  fi

  if [ -n "$IDS" ]; then
    PAYLOAD="$(printf '%s\n' "$IDS" | jq -R . | jq -s '{localIds: ., force: true}')"

    # Silme isteği (targetProjectId ile)
    curl -s -X POST \
      -H "Authorization: Bearer ${ACCESS_TOKEN}" \
      -H "Content-Type: application/json" \
      -d "$PAYLOAD" \
      "${BASE}/accounts:batchDelete?targetProjectId=${PROJECT_ID}" \
      | jq -r '.errors // empty'

    COUNT=$(printf '%s\n' "$IDS" | wc -l | tr -d ' ')
    TOTAL=$((TOTAL + COUNT))
    echo "🗑️  Deleted in this page: $COUNT | Total: $TOTAL"
  fi
done

echo "✅ Done. Approx total deleted: $TOTAL"
