#!/usr/bin/env bash
# Commit everything and push to GitHub. GitHub Actions then builds a new release,
# and every `opioid` launch picks it up automatically.
#
#   ./release.sh "what changed"

set -euo pipefail
cd "$(dirname "$0")"

msg="${1:-Update}"

dotnet build -c Release --nologo -v quiet   # don't push something that doesn't compile

git add -A
if git diff --cached --quiet; then
    echo "Nothing to commit."
else
    git commit -m "$msg"
fi
git push

echo
echo "Pushed. Build progress: https://github.com/$(git remote get-url origin | sed -E 's#.*github.com[:/]##; s#\.git$##')/actions"
