# Manual tests (fork)

Fork-only. Checks the unit tests can't do.

## Install from the fork's repo URL (#1)

1. Open `https://github.com/pearstopher/SpotifyHonorific/releases/latest/download/repo.json` in a browser. It downloads a JSON list with one SpotifyHonorific entry, version `1.18.2.x`.
2. In game, add that URL under `/xlsettings` → Experimental → Custom Plugin Repositories, and save.
3. Uninstall the official SpotifyHonorific, then install SpotifyHonorific from the plugin installer. The installed version shows `1.18.2.x`.
4. `/spotifyhonorific config` opens the config window. Your previous settings and Spotify login are still there.
5. Play a song on Spotify. Your honorific title updates as it did with the official plugin.
