# Opioid Multitool

Console multitool with a matrix-rain intro, typed splash and a title menu.

## Install (Linux)

```sh
curl -fsSL https://raw.githubusercontent.com/furryfemboyuh/opioid/main/install.sh | bash
```

Then run `opioid` from any terminal. Each launch downloads the newest release
(cached in `~/.local/share/opioid/versions`, last 5 kept). If GitHub can't be
reached it runs the newest cached build and the title screen shows a warning.

```
opioid --update     download the newest version without running it
opioid --versions   list cached versions
opioid --uninstall  remove it
```

## Windows

Download `OpioidMultitool-win-x64.exe` from the latest release and run it.

## Releasing

Every push to `main` builds and publishes a new release automatically
(GitHub Actions). From a local clone:

```sh
./release.sh "what changed"
```
