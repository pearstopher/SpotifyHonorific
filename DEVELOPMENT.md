# Development (fork)

Fork-only notes for pearstopher/SpotifyHonorific. Not part of upstream PRs.

## Remotes

- `origin`: https://github.com/pearstopher/SpotifyHonorific (this fork)
- `upstream`: https://github.com/Valiice/SpotifyHonorific

Sync the fork with upstream: `gh repo sync pearstopher/SpotifyHonorific`, then `git pull` on `master`.

## Test builds from this fork

`.github/workflows/fork-release.yml` builds the plugin and publishes a GitHub release with `SpotifyHonorific.zip` and a generated `repo.json`.

- **Version / tag:** upstream's version plus a 4th fork build number, e.g. `v1.18.2.1`, `v1.18.2.2`. These never clash with upstream's 3-part tags, and Dalamud sees them as newer than the matching upstream release.
- **Release:** `git tag v1.18.2.1`, then `git push origin v1.18.2.1` (or run "Fork Release" manually from the Actions tab).
- **Dalamud custom repo URL:**
  `https://github.com/pearstopher/SpotifyHonorific/releases/latest/download/repo.json`
  (always the newest fork release).

Fork Actions settings: upstream's "Build and Release" (`release.yml`) and "Daily Release Scheduler" (`scheduled-bump.yml`) are **disabled** on this fork. The first dispatches to Valiice's plugin repo; the second would push tags that clash with upstream. The files themselves are left unchanged.

## Installing the fork build in game

1. `/xlsettings` → Experimental → Custom Plugin Repositories: add the URL above, tick Enabled, Save.
2. The fork uses the same internal name as the official plugin, so only one can be installed. Uninstall the official SpotifyHonorific first (`/xlplugins` → Installed). Your settings are stored per plugin name and stay.
3. `/xlplugins` → search SpotifyHonorific → install the one from the fork's repo (version 1.18.2.x).

## Troubleshooting

| Problem | Fix |
|---|---|
| Plugin doesn't appear in the installer | Check the repo URL is enabled; open the URL in a browser to confirm `repo.json` downloads. |
| Fork release workflow fails at "Determine Version" | Tag must have 4 parts, e.g. `v1.18.2.1`. |
