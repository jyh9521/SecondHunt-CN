#!/usr/bin/env bash
# Switch one portable install back to the original English text without touching saves.
set -euo pipefail
[[ $# -eq 1 ]] || { printf 'Usage: %s "<directory containing SecondHunt.exe>"\n' "$0" >&2; exit 2; }
root=$(cd -- "$1" && pwd -P)
[[ -f "$root/SecondHunt.exe" && ! -L "$root/SecondHunt.exe" && ! -L "$root/locale.txt" ]] || {
    printf '%s\n' 'Expected an ordinary portable Second Hunt installation.' >&2; exit 2;
}
tmp=$(mktemp "$root/.locale.XXXXXXXX")
trap 'rm -f -- "$tmp"' EXIT
printf 'en\n' > "$tmp"
mv -f -- "$tmp" "$root/locale.txt"
printf '%s\n' 'locale.txt=en; original English restored on next launch; data and saves unchanged.'
