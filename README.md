# Copper Camp

Butte, Montana, 1906. Survive twelve years on "the richest hill on earth" — the mines, the Company, the union and the town — and come out with a house on the Flat or a ticket home.

A Unity 6 (URP) remake of the [Copper Camp browser game](https://github.com/mjw-dodi/copper-camp), with a walkable 3D town, lit mine drifts and short scenes in every place in town.

## Play

**Play in your browser:** https://mjw-dodi.github.io/copper-camp-unity/

Works best in Chrome, Edge or Safari on a computer. The first load is about 50 MB. Your game saves in your browser.

## Open the project in Unity

1. Install **Unity 6.3 LTS (6000.3.28f1)** with **Web Build Support** from Unity Hub.
2. Clone or download this repository, then in Unity Hub choose **Add → Add project from disk** and pick the folder.
3. The first open takes several minutes while Unity rebuilds its cache.
4. Menu **Copper Camp → Build Full Game Scene** sets up the scene; **Copper Camp → Build Web (small)** makes a web build in `WebBuild/`.

Folders: `Assets/Scripts/Game` is the game logic, `Assets/Scripts/Unity` is the 3D world and interface, `Assets/Editor` holds the build tools, and `docs/` is the published web build.

## Credits

- Mine textures: Poly Haven (CC0) — see `Assets/Textures/Mine/CREDITS.txt`.
- Font: DejaVu Serif — see `Assets/Resources/CC/font/LICENSE_DEJAVU.txt`.
- Photos of the town and people are AI-generated in the style of period photographs.
- Sound effects and music: generated with ElevenLabs for the original browser game.
