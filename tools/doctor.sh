#!/usr/bin/env bash
# Sombra · tools/doctor.sh
# Confere as ferramentas de desenvolvimento do monorepo e diz como instalar o que falta.
# Só lê o ambiente: não instala nem altera nada.
#
# Uso: tools/doctor.sh
# Saída: 0 se todas as ferramentas obrigatórias estão ok; 1 caso contrário.
set -Eeuo pipefail

if [[ -t 1 ]]; then
  ok=$'\e[32m✓\e[0m' bad=$'\e[31m✗\e[0m' warn=$'\e[33m!\e[0m'
else
  ok='OK' bad='FALTA' warn='AVISO'
fi

missing_required=0
missing_optional=0

# Primeira versão no formato N.N[.N] encontrada no texto.
extract_version() { grep -oE '[0-9]+(\.[0-9]+)+' | head -n1; }

# version_ge ATUAL MINIMA → 0 se ATUAL >= MINIMA.
version_ge() { [[ "$(printf '%s\n%s\n' "$2" "$1" | sort -V | head -n1)" == "$2" ]]; }

# report OBRIGATORIA(sim|nao) NOME DETALHE DICA
report_missing() {
  local required=$1 name=$2 detail=$3 hint=$4
  if [[ $required == sim ]]; then
    printf '  %s %-14s %s\n      → %s\n' "$bad" "$name" "$detail" "$hint"
    missing_required=$((missing_required + 1))
  else
    printf '  %s %-14s %s\n      → %s\n' "$warn" "$name" "$detail" "$hint"
    missing_optional=$((missing_optional + 1))
  fi
}

# check OBRIGATORIA COMANDO MINIMA FASES DICA [COMANDO_DE_VERSAO...]
check() {
  local required=$1 cmd=$2 min=$3 phases=$4 hint=$5
  shift 5
  if ! command -v "$cmd" >/dev/null 2>&1; then
    report_missing "$required" "$cmd" "ausente (usado em $phases)" "$hint"
    return
  fi
  local version
  version=$("${@:-$cmd}" 2>&1 | extract_version || true)
  if [[ -z $version ]] || ! version_ge "$version" "$min"; then
    report_missing "$required" "$cmd" "versão ${version:-desconhecida}, precisa de ≥ $min ($phases)" "$hint"
    return
  fi
  printf '  %s %-14s %s\n' "$ok" "$cmd" "$version"
}

echo "Sombra · doctor"
echo
echo "Obrigatórias"
check sim git 2.40 "todas" "sudo pacman -S git" git --version
check sim docker 24.0 "F0.10, F1, F3" "sudo pacman -S docker docker-compose && sudo systemctl enable --now docker" docker --version
if command -v docker >/dev/null 2>&1; then
  if docker info >/dev/null 2>&1; then
    printf '  %s %-14s %s\n' "$ok" "docker daemon" "rodando"
  else
    report_missing sim "docker daemon" "parado ou sem permissão" "sudo systemctl start docker (e o usuário no grupo docker)"
  fi
  compose=$(docker compose version 2>/dev/null | extract_version || true)
  if [[ -n $compose ]] && version_ge "$compose" 2.20; then
    printf '  %s %-14s %s\n' "$ok" "docker compose" "$compose"
  else
    report_missing sim "docker compose" "versão ${compose:-ausente}, precisa de ≥ 2.20 (F0.10)" "sudo pacman -S docker-compose"
  fi
fi
if command -v dotnet >/dev/null 2>&1 && dotnet --list-sdks 2>/dev/null | grep -qE '^10\.'; then
  printf '  %s %-14s %s\n' "$ok" "dotnet sdk" "$(dotnet --list-sdks | grep -E '^10\.' | tail -n1 | cut -d' ' -f1)"
else
  report_missing sim "dotnet sdk" "SDK 10 ausente (F1, F2)" "sudo pacman -S dotnet-sdk (ou o instalador da Microsoft)"
fi
check sim java 21 "F3" "sudo pacman -S jdk21-openjdk" java -version
check sim node 22 "F6, F7, F13" "sudo pacman -S nodejs npm" node --version
check sim uv 0.4 "F0.11, F0.15, F4" "sudo pacman -S uv (o uv instala o Python 3.12 do projeto)" uv --version
check sim gh 2.40 "F0.5, F0.12, F0.14" "sudo pacman -S github-cli" gh --version
if command -v gh >/dev/null 2>&1; then
  if gh auth status >/dev/null 2>&1; then
    printf '  %s %-14s %s\n' "$ok" "gh auth" "$(gh api user --jq .login 2>/dev/null || echo logado)"
  else
    report_missing sim "gh auth" "não logado (F0.5)" "gh auth login"
  fi
fi
check sim jq 1.6 "scripts e CI" "sudo pacman -S jq" jq --version
check sim shellcheck 0.9 "F0.12, F8" "sudo pacman -S shellcheck" shellcheck --version

echo
echo "Opcionais (só na fase indicada)"
check nao VBoxManage 7.0 "F8" "sudo pacman -S virtualbox" VBoxManage --version
check nao vagrant 2.4 "F8" "fora dos repositórios oficiais do Arch: yay -S vagrant (AUR); decidido na F8.1" vagrant --version
check nao ffmpeg 6.0 "F13" "sudo pacman -S ffmpeg" ffmpeg -version
if command -v gitleaks >/dev/null 2>&1; then
  printf '  %s %-14s %s\n' "$ok" "gitleaks" "presente"
else
  report_missing nao gitleaks "ausente (pre-commit local; o CI já roda)" "sudo pacman -S gitleaks"
fi
check nao psql 14 "depuração do banco" "sudo pacman -S postgresql-libs" psql --version

echo
if ((missing_required == 0)); then
  printf '%s Obrigatórias ok. Opcionais faltando: %d.\n' "$ok" "$missing_optional"
  exit 0
fi
printf '%s Faltam %d obrigatória(s). Opcionais faltando: %d.\n' "$bad" "$missing_required" "$missing_optional"
exit 1
