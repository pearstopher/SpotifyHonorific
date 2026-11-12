# Manual tests (fork)

Fork-only. Checks the unit tests can't do.

## Install from the fork's repo URL (#1)

1. Open `https://github.com/pearstopher/SpotifyHonorific/releases/latest/download/repo.json` in a browser. It downloads a JSON list with one SpotifyHonorific entry, version `1.18.2.x`.
2. In game, add that URL under `/xlsettings` → Experimental → Custom Plugin Repositories, and save.
3. Uninstall the official SpotifyHonorific, then install SpotifyHonorific from the plugin installer. The installed version shows `1.18.2.x`.
4. `/spotifyhonorific config` opens the config window. Your previous settings and Spotify login are still there.
5. Play a song on Spotify. Your honorific title updates as it did with the official plugin.

## Load a local dev build (#3)

1. Run `dotnet build` in the project folder. It ends with "Build succeeded".
2. In game, add the dev plugin path from DEVELOPMENT.md under `/xlsettings` → Experimental → Dev Plugin Locations, and save.
3. `/xlplugins` → Installed: disable the SpotifyHonorific installed from your repo URL.
4. `/xlplugins` → Dev Tools → Installed Dev Plugins: enable SpotifyHonorific and turn on auto-reload. It shows version `1.18.1.0`.
5. Play a song. Your title updates as before.
6. Run `dotnet build` again. Within a few seconds the dev plugin reloads (a reload line appears in `/xllog`), and the title still updates.
7. Disable the dev plugin and re-enable the installed one, so you're back on the repo build.
