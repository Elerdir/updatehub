# Integrating UpdateHub into Tauri apps

Applies to: **container-commander**.

Tauri has a built-in updater that speaks directly to the UpdateHub Tauri endpoint — no SDK needed.

## 1. Register the app in UpdateHub

1. Open UpdateHub admin
2. Applications → **+ New Application**
3. Slug: `container-commander`
4. Name: `Container Commander`

## 2. Configure the Tauri updater

In `src-tauri/tauri.conf.json` (Tauri v2):

```json
{
  "plugins": {
    "updater": {
      "active": true,
      "endpoints": [
        "https://your-updatehub-server.com/api/apps/container-commander/tauri/latest.json"
      ],
      "dialog": true,
      "pubkey": "YOUR_TAURI_PUBLIC_KEY"
    }
  }
}
```

For **Tauri v1**:
```json
{
  "tauri": {
    "updater": {
      "active": true,
      "endpoints": [
        "https://your-updatehub-server.com/api/apps/container-commander/tauri/latest.json"
      ],
      "dialog": true,
      "pubkey": "YOUR_TAURI_PUBLIC_KEY"
    }
  }
}
```

## 3. Sign your builds

Tauri requires every artifact to have an Ed25519 signature. Generate the signing keys once:

```bash
npm run tauri signer generate -- --output ~/.tauri/container-commander.key
```

This creates a private key (`*.key`) and a public key (`*.key.pub`).
- Paste the **public key** into `tauri.conf.json` as `pubkey`
- Keep the **private key** secret — store it as `TAURI_PRIVATE_KEY` in GitHub Secrets

## 4. Upload from GitHub Actions

```yaml
# .github/workflows/release.yml
- name: Build Tauri app
  run: npm run tauri build
  env:
    TAURI_PRIVATE_KEY: ${{ secrets.TAURI_PRIVATE_KEY }}
    TAURI_KEY_PASSWORD: ${{ secrets.TAURI_KEY_PASSWORD }}

- name: Upload to UpdateHub
  run: |
    INSTALLER="src-tauri/target/release/bundle/nsis/container-commander_${{ github.ref_name }}_x64-setup.exe"
    SIGNATURE=$(cat "${INSTALLER}.sig")

    curl -f -X POST "${{ secrets.UPDATEHUB_URL }}/api/ci/apps/container-commander/releases" \
      -H "X-UpdateHub-Token: ${{ secrets.UPDATEHUB_CI_TOKEN }}" \
      -F "file=@${INSTALLER}" \
      -F "signature=${SIGNATURE}" \
      -F "version=${{ github.ref_name }}" \
      -F "platform=windows" \
      -F "arch=x64" \
      -F "channel=stable" \
      -F "release_notes=${{ github.event.release.body }}"
```

## 5. What UpdateHub returns

The `/api/apps/container-commander/tauri/latest.json` endpoint returns:

```json
{
  "version": "1.1.0",
  "notes": "Bug fixes",
  "pub_date": "2026-05-09T12:00:00Z",
  "platforms": {
    "windows-x86_64": {
      "signature": "dW50cnVzdGVkIGNvbW1lbnQ...",
      "url": "https://your-server/api/downloads/some-guid"
    }
  }
}
```

Tauri verifies the Ed25519 signature before applying the update.

## Notes

- The artifact must be uploaded **with a signature** — otherwise Tauri won't show the update
- Publish the release in the admin UI after uploading all platform builds
- Test with `npm run tauri dev` and a lower version number in `tauri.conf.json`
