# SaveGameBackup for SULFUR 0.9.6

**This mod implements a save game backup menu.**

Works fine on my end so far, but I take no responsibility for anything (ever lmao).

Almost 800 lines of code. Yeeeesh. haha.

---

## Requirements and setup

It's pretty bog-standard bepinex Unity modding, but we'll run through the setup just in case you're new to this.

1. Grab `BepInEx-Unity.Mono-win-x64-6.0.0-pre.2.zip` from the [releases section of their github](https://github.com/BepInEx/BepInEx/releases/download/v6.0.0-pre.2/BepInEx-Unity.Mono-win-x64-6.0.0-pre.2.zip). (Direct download link).
2. Extract that into the game's root folder.
3. Run the game once to generate the necessary folders.
4. Download `SaveGameBackup.zip` from [the releases section of this github repo](https://github.com/remghoost/SaveGameBackup/releases/).
5. Extract `SaveGameBackup.zip` into `SULFUR/BepInEx/Plugins`.
6. Done!

---
## Building from source

If you're not comfortable downloading a random `.dll` from a stranger on the internet (which, I don't blame you), you can build this plugin from source.

1. `git clone` this repo.
2. Retarget `UnityEngine.UI.dll` and `Unity.TextMeshPro.dll` in `SaveGameBackup.csproj` to wherever your game exists.
3. `dotnet build`

---
## TODO
1. Allow renaming of save files in-game.
2. Drink more coffee.
---
## Considerations
- Shoutout to ChatGPT and Claude3.5 for always being homies. <3
- I probably didn't get every edge case (though I got a bunch of them). 
- Submit an issue if you find something wonky.
- **This mod might break some things (it shouldn't though) and comes with no warranty!**