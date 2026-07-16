# advancemint (linux) 🏆

minecraft-style "Advancement Made!" toasts for stuff your pc actually does. stay up
past 2am? achievement. open discord? achievement. battery hits 100%? achievement.
~55 of them.

built for kubuntu/kde but should work on any modern linux desktop.

## install

```
tar -xzf advancemint-linux-x64.tar.gz
cd advancemint
chmod +x advancemint          # tarballs from windows lose the exec bit
./advancemint
```

it lives in your system tray. right click it for:
- **Show progress** - the full achievement list (unlocked in color, locked greyed out)
- **Test toast** - preview a random one
- **Reset progress** / **Exit**

progress saves to `~/.config/advancemint/state.json`.

## optional: music achievements

the media achievements (Now Playing, Resident DJ, Audiophile, Just One More Song)
read whatever's playing via MPRIS. that needs playerctl:

```
sudo apt install playerctl
```

works with spotify, cider, browsers, vlc, anything MPRIS. without playerctl the music
achievements just never fire, nothing else breaks.

## want it to start automatically?

```
mkdir -p ~/.config/autostart
cp advancemint.desktop ~/.config/autostart/
```
(edit the `Exec=` line in the .desktop first so it points at wherever you put it)

## what's different from the windows version

some windows achievements can't exist here, because wayland (correctly) doesn't let
random apps snoop on your windows:
- no idle/AFK achievements
- no "which app is focused" or window-count achievements

so linux gets its own instead: **Penguin Power**, **Uptime Warrior** (24h uptime),
**Never Reboot** (7 days), **Load Bearing** / **Thermal Throttle** (load average), and
KDE app achievements (Konsole, Dolphin, Kate, system monitor, System Settings).

## notes

- emoji icons need a color emoji font. kubuntu normally ships `fonts-noto-color-emoji`.
  if the icons look like boxes: `sudo apt install fonts-noto-color-emoji`
- toast sound uses `paplay` (pulse/pipewire), falling back to `aplay`/`ffplay`.
- this was cross-compiled from windows and **has not been tested on real hardware yet**,
  so if something looks off, screenshot it and it'll get fixed.
