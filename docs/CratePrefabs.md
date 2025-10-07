# Rust Loot Container Prefab Reference

The table below links the prefab short names that RandomFarm listens for with the configuration keys they map to by default, along with an external RustLabs reference that shows the crate's in-game appearance. Each link includes an image of the container on the destination page.

| Prefab short name | Default config key | In-game container | Reference |
| --- | --- | --- | --- |
| `crate_normal` | `crate_normal` | Standard wooden crate | https://rustlabs.com/entity/crate-normal |
| `crate_normal_2` | `weapon_crate` | Weapon crate (yellow hazard stripes) | https://rustlabs.com/entity/crate-normal-2 |
| `crate_normal_2_food` | `crate_normal` | Food crate | https://rustlabs.com/entity/crate-normal-2-food |
| `crate_normal_2_medical` | `crate_normal` | Medical crate | https://rustlabs.com/entity/crate-normal-2-medical |
| `crate_normal_2_tools` | `crate_tools` | Tool crate | https://rustlabs.com/entity/crate-normal-2-tools |
| `crate_military` | `crate_military` | Military crate | https://rustlabs.com/entity/crate-military |
| `crate_military_2` | `crate_military` | Advanced military crate | https://rustlabs.com/entity/crate-military-2 |
| `crate_elite` | `weapon_crate` | Elite crate | https://rustlabs.com/entity/crate-elite |
| `crate_underwater_advanced` | `weapon_crate` | Sunken advanced crate | https://rustlabs.com/entity/crate-underwater-advanced |
| `bradley_crate` | `heli_crate` | Bradley APC crate | https://rustlabs.com/entity/bradley-crate |
| `chinookcrate` | `heli_crate` | Locked crate (Chinook) | https://rustlabs.com/entity/chinook-crate |
| `cargocrate` | `heli_crate` | Cargo ship elite crate | https://rustlabs.com/entity/cargo-crate |
| `crate_tunnel` | `crate_normal` | Train tunnel crate | https://rustlabs.com/entity/crate-tunnel |
| `crate_basic` | `crate_normal` | Small roadside crate | https://rustlabs.com/entity/crate-basic |

> **Tip:** If you need to route a prefab to a different loot table, edit `PrefabAliases` in `RandomFarm.json` so the prefab short name points at the configuration section you prefer.
