#!/usr/bin/env bash
#
# Push the current Builds/WebGL output to the `deploy` branch, which Render serves.
#
# Uses a throwaway git worktree so the main working tree is never touched: no
# checkout switching, no cleaning, nothing to lose if this fails halfway.
#
set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD="$REPO/Builds/WebGL"
WT="$(mktemp -d -t lm-deploy-XXXXXX)"

if [ ! -f "$BUILD/index.html" ]; then
  echo "No build found at $BUILD" >&2
  echo "Build it first:  Unity menu > Laundry Monster > Build WebGL" >&2
  exit 1
fi

cleanup() {
  cd "$REPO"
  git worktree remove --force "$WT" >/dev/null 2>&1 || true
  rm -rf "$WT" >/dev/null 2>&1 || true
}
trap cleanup EXIT

cd "$REPO"
git fetch origin deploy >/dev/null 2>&1 || true

if git show-ref --verify --quiet refs/remotes/origin/deploy; then
  git worktree add --detach "$WT" origin/deploy >/dev/null
  cd "$WT"
  git checkout -B deploy >/dev/null
  # Drop the previous build so removed files do not linger.
  git rm -rq --cached . >/dev/null 2>&1 || true
  find . -mindepth 1 -maxdepth 1 ! -name '.git' -exec rm -rf {} +
else
  git worktree add --orphan -b deploy "$WT" >/dev/null
  cd "$WT"
fi

cp -r "$BUILD/." "$WT/"

# Plain bytes only: an LFS pointer would be served verbatim and the game would
# fail to load.
printf '* -filter -diff -merge -text\n' > "$WT/.gitattributes"

git add -A
if git diff --cached --quiet; then
  echo "Deploy branch already matches this build; nothing to push."
  exit 0
fi

git commit -q -m "Deployed WebGL build $(date -u +%Y-%m-%dT%H:%M:%SZ)"
git push -q origin deploy
echo "Pushed to deploy. Render will redeploy automatically."
