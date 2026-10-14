# Napojení Tauri 2 aplikace na UpdateHub

Platí pro: **container-commander**, **erato**. Ti dva jsou zároveň referenční
implementace — `release.yml`, `src/lib/updater.ts` a `scripts/release-notes.sh`.

Tauri má vestavěný updater, který mluví přímo s Tauri endpointem UpdateHubu —
žádné SDK není potřeba. Celkový tok vydání (Dispatch → `dispatch.yml` →
`release.yml` → UpdateHub draft → Publish) popisuje [INTEGRATE.md](INTEGRATE.md).

## 1. Registrace v UpdateHubu

1. Admin → Applications → **+ New Application**
2. Slug = slug repa (malá písmena, pomlčky), stejný všude — např. `erato`
3. Na detailu aplikace vygeneruj **per-app CI token**

## 2. Podpisový klíč

Updater instaluje jen balíčky podepsané klíčem, jehož veřejnou část má
zapečenou v aplikaci. Klíč se generuje jednou:

```bash
pnpm tauri signer generate -w ~/.tauri/<slug>.key -p "<heslo>" --ci
```

Vznikne `<slug>.key` (privátní) a `<slug>.key.pub` (veřejný).

> **Ztráta privátního klíče nebo hesla = konec aktualizací** pro všechny
> stávající instalace — nová verze s jiným klíčem se u nich neověří.

## 3. Konfigurace aplikace (`src-tauri/tauri.conf.json`)

```json
{
  "bundle": {
    "createUpdaterArtifacts": true
  },
  "plugins": {
    "updater": {
      "endpoints": [
        "https://updatehub.niderle.cz/api/apps/<slug>/tauri/latest.json"
      ],
      "pubkey": "<obsah souboru <slug>.key.pub>"
    }
  }
}
```

- Endpoint **musí být https**, jinak release build spadne.
- Tauri 2 nemá vestavěný dialog (`"dialog": true` je Tauri 1) — UI si aplikace
  dělá sama přes `@tauri-apps/plugin-updater` (`check()`,
  `downloadAndInstall()`) a `@tauri-apps/plugin-process` (`relaunch()`).

Rust: `tauri-plugin-updater` a `tauri-plugin-process`, registrace
`.plugin(tauri_plugin_updater::Builder::new().build())` a
`.plugin(tauri_plugin_process::init())`. Capabilities: `updater:default`,
`process:allow-restart`.

S `createUpdaterArtifacts` lokální build bez klíče skončí chybou podpisu —
lokální skript buď klíč načte (`TAURI_SIGNING_PRIVATE_KEY_PATH` +
`TAURI_SIGNING_PRIVATE_KEY_PASSWORD`), nebo předá `--config` s
`"createUpdaterArtifacts": false`.

## 4. Secrets v repu

| Secret | Hodnota |
|---|---|
| `TAURI_SIGNING_PRIVATE_KEY` | obsah `<slug>.key` |
| `TAURI_SIGNING_PRIVATE_KEY_PASSWORD` | heslo ke klíči |
| `UPDATEHUB_URL` | `https://updatehub.niderle.cz` |
| `UPDATEHUB_CI_TOKEN` | per-app CI token z kroku 1 |

Nastavuj přes `gh secret set NAME --body "…"` — roura z PowerShellu 5.1 přidá CRLF.
Názvy `TAURI_PRIVATE_KEY` / `TAURI_KEY_PASSWORD` jsou z Tauri 1 a Tauri 2 je ignoruje.

## 5. Upload z GitHub Actions

`tauri-action` (nebo `pnpm tauri build`) s proměnnými
`TAURI_SIGNING_PRIVATE_KEY` + `_PASSWORD` vyrobí vedle instalátorů i `.sig`.
Každý soubor se nahrává zvlášť; **stejná verze = další artefakt téhož draftu**.

```bash
curl -f -X POST "$UPDATEHUB_URL/api/ci/apps/<slug>/releases" \
  -H "X-UpdateHub-Token: $UPDATEHUB_CI_TOKEN" \
  -F "file=@<instalátor>" \
  -F "signature=$(cat <instalátor>.sig)" \
  -F "version=1.2.0" \
  -F "platform=windows" \
  -F "arch=x64" \
  -F "channel=stable" \
  -F "release_notes=<release-notes.md"
```

- `release_notes=<soubor` posílá obsah souboru — text z `CHANGELOG.md`, ne odkaz
  na GitHub (repa bývají soukromá).
- **Soubor, který stahuje updater, nahraj poslední** (Windows NSIS `.exe`,
  macOS `.app.tar.gz`, Linux `.AppImage`). Instalátory pro lidi (`.msi`, `.dmg`,
  `.deb`) mohou jít bez podpisu — do manifestu updateru se nezařadí, ale
  www.niderle.cz je nabídne ke stažení.
- Cesty: ve Cargo workspace je bundle v `target/[<target>/]release/bundle/`,
  ne v `src-tauri/target/`.

### Platformy a architektury

| `platform` / `arch` | Klíč v `latest.json` | Typický soubor |
|---|---|---|
| `windows` / `x64` | `windows-x86_64` | `*-setup.exe` (NSIS) |
| `macos` / `arm64` | `darwin-aarch64` | `*.app.tar.gz` |
| `macos` / `x64` | `darwin-x86_64` | `*.app.tar.gz` |
| `linux` / `x64` | `linux-x86_64` | `*.AppImage` |

**Univerzální macOS build** (`--target universal-apple-darwin`) běží na obou
architekturách, ale updater se ptá podle konkrétní — nahraj tentýž
`.app.tar.gz` dvakrát, jednou s `arch=x64` a jednou s `arch=arm64`.

Má-li platforma víc podepsaných souborů, manifest vybere deterministicky:
NSIS / `.app.tar.gz` / AppImage před MSI (MSI aktualizace přes NSIS instalaci
by nechala dvě instalace vedle sebe), při shodě novější upload.

## 6. Co UpdateHub vrací

`GET /api/apps/<slug>/tauri/latest.json` — jen **publikovaná** verze, jen
artefakty s podpisem:

```json
{
  "version": "1.2.0",
  "notes": "Co je nového…",
  "pub_date": "2026-10-12T12:00:00Z",
  "platforms": {
    "windows-x86_64": {
      "signature": "dW50cnVzdGVkIGNvbW1lbnQ…",
      "url": "https://updatehub.niderle.cz/api/downloads/<id>"
    }
  }
}
```

Dokud není publikovaná žádná verze (nebo slug neexistuje), endpoint vrací
**404**. Tauri 2 updater ho bere jako chybu („Could not fetch a valid release
JSON"), za „žádná aktualizace" považuje jen 204 nebo manifest se stejnou
verzí. Tichou kontrolu při startu proto obal `catch`; ruční kontrola chybu
před prvním vydáním ukáže.

## Poznámky

- Draft se uživatelům nabídne až po **Publish** v admin UI, až dorazí všechny
  platformy.
- Lokální test: `pnpm tauri dev` s nižší verzí v `tauri.conf.json`, než je
  poslední publikovaná.
