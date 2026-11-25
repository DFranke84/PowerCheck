# PowerCheck

A small Windows app that answers **"Is this OK to plug in?"** before you plug things in:
which extension cord to use, whether a strip, hub or circuit can handle everything you're
plugging into it, and how many strings of Christmas lights you can chain.

Every answer is **green / yellow / red** with a plain-language reason. It assumes US
household power (120 V outlets, 15 A or 20 A breakers).

> PowerCheck is a guide, not a substitute for the device's manual. When in doubt, ask an electrician.

## The four tabs

| Tab | What it does |
|---|---|
| **Extension cord** | Pick what you'll plug in (or type the watts/amps from its label), the cord length and indoors/outdoors. It tells you the thinnest cord that's safe, checks every gauge (16, 14, 12, 10) and shows the longest safe length for each. The **Christmas lights helper** is at the bottom: string type, how many, the "connect up to" number from the tag, and the cord length. |
| **Power strip / circuit** | Build it the way it's plugged in: **Step 1** the wall outlet (15 A or 20 A breaker), **Step 2** what plugs into the wall, in order (extension cord, power strip, your hubs and splitters), **Step 3** the devices at the end. Every link is checked; the weakest one decides the answer. It also warns about strips plugged into cords, stacked adapters, too many plugs for the outlets, and devices that must go straight into the wall. |
| **Devices** | Your lists, with pictures and shop links. **Devices** on top (search, groups, ★ favorites) and **Strips, hubs & cords** below. This is the only tab that saves anything. |
| **Learn** | Short topics in plain words (watts and amps, cords, strips, breakers, motors, outdoors, Christmas lights), a warning-signs checklist and quick numbers. |

## What gets saved, and where

Everything lives next to `PowerCheck.exe`, so the folder can be copied to another PC or a USB stick.

| File / folder | What it holds | Changed by the app? |
|---|---|---|
| `devices.json` | The built-in device list (about 85 devices) | Never |
| `gear.json` | The built-in strips, hubs and cords | Never |
| `my-devices.json` | Devices you added or edited on the Devices tab | Yes |
| `my-gear.json` | Strips, hubs and cords you added or edited | Yes |
| `favorites.json` | Your ★ favorites | Yes |
| `device-images\` | Product pictures; `thumbs\` holds small copies the app makes itself | Yes (your own pictures) |

Quick adds on the Extension cord and Power strip tabs ("just checking") are never saved.
Editing a built-in device saves your own copy; **Undo my edit** puts the original back.
Pictures you replace or remove go to the Recycle Bin.

## The rules it uses

- Watts = amps × 120. Labels showing VA are treated as watts.
- Cord ratings: 16 gauge 13 A, 14 gauge 15 A, 12 gauge 20 A, 10 gauge 25 A, never more than the 15 A plug.
- Voltage drop over the cord: green up to 3 %, yellow up to 5 %, red above.
- Green up to 80 % of a rating, yellow up to 100 %, red over. Things that run for hours should stay in the green.
- Devices whose makers say "plug straight into the wall" (heaters, fridges, AC units, big compressors…) are red on any cord or strip.

## Running it

Copy the `PowerCheck` folder anywhere and double-click `PowerCheck.exe`. Nothing to install:
the .NET runtime is built in.

## Building from source

Needs the .NET 8 SDK on Windows.

```powershell
dotnet test PowerCheck.Tests
dotnet publish PowerCheck.App -c Release -r win-x64 -o publish
```

`publish\` then holds `PowerCheck.exe` plus `devices.json`, `gear.json` and `device-images\`.

Product pictures are not stored in git (they belong to their makers). A fresh clone runs fine
without them: devices show a small symbol instead, and you can add your own pictures on the Devices tab.

## Projects

- `PowerCheck.Core`: the rules (cord sizing, loads, chains, Christmas lights) with no UI.
- `PowerCheck.App`: the WPF window.
- `PowerCheck.Tests`: xUnit tests for the rules.
