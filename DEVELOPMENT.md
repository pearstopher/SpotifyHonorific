# Development (fork)

Fork-only notes for pearstopher/SpotifyHonorific. Not part of upstream PRs.

## Remotes

- `origin`: https://github.com/pearstopher/SpotifyHonorific (this fork)
- `upstream`: https://github.com/Valiice/SpotifyHonorific

Sync the fork with upstream: `gh repo sync pearstopher/SpotifyHonorific`, then `git pull` on `master`.

## One-time setup

- .NET 10 SDK (`dotnet --list-sdks` shows `10.0.x`).
- XIVLauncher with Dalamud installed and run at least once. The Dalamud SDK finds it at `%APPDATA%\XIVLauncher\addon\Hooks\dev\` automatically; set `DALAMUD_HOME` only if Dalamud lives somewhere else.
- In game, `/xlsettings` → Experimental → **Dev Plugin Locations**: add
  `C:\Users\snows\Documents\VSCode\ffxiv-spotify-honorific-fork\SpotifyHonorific\bin\Debug\SpotifyHonorific.dll`, then Save.

## Build and test

- `dotnet build`: builds the plugin and the tests (Debug). Output: `SpotifyHonorific\bin\Debug\` (`SpotifyHonorific.dll` + `SpotifyHonorific.json`).
- `dotnet test`: runs the xUnit tests in `SpotifyHonorific.Tests`.

## Edit-and-test loop

1. Edit code, then run `dotnet build` and `dotnet test` (Claude does this; the in-game checks are mine).
2. In game: `/xlplugins` → Dev Tools → Installed Dev Plugins → SpotifyHonorific. Turn on **auto-reload** once; after that, every build reloads the plugin automatically. (Otherwise disable and re-enable it.)
3. Only one SpotifyHonorific can be loaded at a time. Keep any installed copy disabled or uninstalled; all testing uses the dev build.
4. To confirm a new build is running, check `dalamud.log` for the dev plugin's reload lines (`Unloading` / `Finished loading SpotifyHonorific`) after the build time. The dev build's version is the csproj `<Version>` (currently `1.18.1.0`), which doesn't change per build.

## Logs

- `%APPDATA%\XIVLauncher\dalamud.log` (previous session: `dalamud.old.log`). In game, `/xllog` shows the same log live.
- Plugin lines are tagged `[SpotifyHonorific]`; debug lines (e.g. `POLLING NOW`) need the log level set to Debug in `/xllog`.

## Test builds from this fork (optional, not used for testing)

Testing uses the dev plugin above. This release path exists if I ever want an installable build.

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
| Build error about Dalamud references | Dalamud isn't at `%APPDATA%\XIVLauncher\addon\Hooks\dev\`: run the game once with Dalamud, or set `DALAMUD_HOME`. |
| Dev plugin won't load / "already loaded" | Disable the installed SpotifyHonorific first; both share the same internal name. |
| Change didn't take effect | Check the version in `/xlplugins` and that auto-reload is on; rebuild. |
