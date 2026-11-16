# Opioid Multitool

Console multitool with a matrix-rain intro, typed splash and a title menu.
There's also a desktop GUI (Avalonia) with the same menu and tools, in a
glassy, glowing, animated window — see below.

## Install (Linux)

```sh
curl -fsSL https://raw.githubusercontent.com/furryfemboyuh/opioid/main/install.sh | bash
```

Then run `opioid` from any terminal. Each launch downloads the newest release
(cached in `~/.local/share/opioid/versions`, last 5 kept). If GitHub can't be
reached it runs the newest cached build and the title screen shows a warning.

```
opioid --gui         launch the desktop GUI instead of the terminal UI
opioid --update      download the newest version without running it
opioid --versions    list cached versions
opioid --uninstall   remove it
```

## Windows

Download `OpioidMultitool-win-x64.exe` from the latest release and run it.
Launch it with `--gui` (e.g. a shortcut target of
`OpioidMultitool-win-x64.exe --gui`) for the desktop GUI instead of the
console UI.

## GUI

`opioid --gui` opens a frameless, custom-themed desktop window built with
[Avalonia](https://avaloniaui.net) — the same main menu and website tester
("DDOS") as the terminal version, with animated blurred glow blobs, glass
panels, a custom display font (Orbitron) and monospace font (JetBrains Mono),
and glowing/animated buttons. It runs on both Linux and Windows from the same
single binary as the TUI.

## Releasing

Every push to `main` builds and publishes a new release automatically
(GitHub Actions). From a local clone:

```sh
./release.sh "what changed"
```
