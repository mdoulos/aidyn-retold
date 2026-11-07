# Places

The remade game names where the player is, the user's design (Nov 7, 2026): "Gwernia" the moment he steps into its streets, "Oisin Forest" on the road between it and Erromon. The original has no such thing: its world map shows the party's ring and the towns' names, and nothing says where the party stands. The port keeps the original's chunks, map sets and world map regions as they are, and lays the names over them in data.

**The record**, `acre:place/<name>` (`Plugins/Records/PlaceRecord.cs`, any plugin may add one): a `name`, and what it names, any or all of `chunks` (chunks of a map set, each by name, "M07", as `ChunkEntry.Name` has them), `regions` (world map regions by their flags, `aidyn:flag/788` to `805`: every chunk carrying one as its `region`, [ui](ui.md#the-world-tab)), and `mapSets` (whole map sets, a town's interiors).

**The rule** (`PlaceRecord.At`): the player is in the first place, in ID order, that names his chunk itself; else the first naming the chunk's region; else the first naming its map set; else nowhere named, and the game says nothing. So a town's two chunks name the town inside the forest's region, and a town's interiors name the town by their map set. `TrekLoop.Location` reads it for the chunk the player stands in, and `GameSession.Location` for the game; the camp screen's "Current Location" line shows it ([camping](camping.md#the-camp-screen)), the campsite's name where nothing names the chunk. A cut-scene's stage has none.

**The ACRE plugin's places** (`records/places.json`), found from the original's data: the towns' Overworld chunks are the ones on the town terrains, 19 and 23 (which refuse camping, [camping](camping.md#where)), told apart by who stands in them.

| Place | Names |
| --- | --- |
| Gwernia | Overworld M06 and M07 (terrain 19; the user's save camps outside it in M08), and the castle's and the town's interiors, map sets 4 and 5. |
| Erromon | Overworld B09, C08 and C09 (King Txomin's palace is in C08), and its interiors, map set 7. |
| Talewok | Overworld H13 and I13 (the wizard school's door warden is in H13). |
| Port Saiid | Overworld C15 (the docks, where the ship's scenes come back to port; C14 beside it is the crossroads whose signs point to Talewok and Port Saiid), and its interiors, map set 2. |
| Terminor | Map set 8. |
| Ugarit | Map sets 13 and 14. |
| Chaos Island | Map set 15. |
| Oisin Forest | World map regions 1 and 2 (`aidyn:flag/788`, `789`): the Overworld from Gwernia north to Erromon, the two towns' chunks excepted. The user gave the name with a question mark; it's the one to change if the lore says otherwise. |

Not named yet, for the user to name: the other sixteen regions of the Overworld (the road east and south, the desert, the island chunks the ship's scenes play in, A22, B22 and E22), and the lesser map sets, the Goblin Camp (1), the Lighthouse (3), the miscellaneous interiors (6), the Barrows (9), Roog's cave (10), the Jundar's interiors (11), Ehud Under (12) and the battlefields (16).

**Finding a chunk.** The world map's ring is at the Overworld chunk's row and column (`WorldMapRecord.Origin`, `Step`), and the towns' names are drawn where the record puts them, near but not on their chunks. A chunk's NPCs say where it is: read the first lines of the scripts its map's dialogue objects start.
