# Suburban Parking — 500 levels

Open `Assets/Scenes/SuburbanParking.unity` and press Play. The scene is first in Build Settings. The original Gameplay scene is preserved.

## Level architecture

`Assets/Parking/LevelCatalog.asset` defines 500 deterministic levels from 10 editable layout prefabs and 50 difficulty tiers. These are generated variations of the original ten challenges, rather than 500 individually hand-authored maps.

- `Assets/Parking/Prefabs/Neighborhood.prefab` holds the shared street, houses, trees, driveways, and fences. It stays in the scene.
- `Assets/Parking/Prefabs/Layout01.prefab` through `Layout10.prefab` contain each challenge's spawn, parking bay, obstacles, and rules.
- `ParkingLevelCatalog` instantiates only the selected layout. It applies reproducible seeded shifts, mirrored approaches, spawn variations, and progressively tighter parking requirements. No Unity global random state is changed.
- Restart reuses the current layout. Changing levels deactivates and destroys the old layout before activating the next. At most one course is active.
- The scene references the catalog directly, and the catalog references its prefabs. Unity includes them in builds; no Resources string lookup or 500 copied scenes is required.

Levels 1–10 preserve the original challenges: straight parking, driveway arrival, right-angle approach, reverse bay, garden slalom, narrow gate, parallel parking, offset alley, tight reverse, and final precision. Each subsequent group of ten revisits those layouts at a harder tier. Difficulty ramps per layout across tiers; challenge types still vary within each group. The final tier uses a 2.45 x 4.85 metre bay, 5-degree heading tolerance, and two allowed contacts. Timers never fall below 90 seconds.

Edit the catalog's seed and count to configure the sequence, or edit layout prefabs to change the underlying courses. Keep the 1.8 x 3.8 metre car footprint, turning clearance, and bay's unit scale in mind. Run the full generator validation after changes.

## Playing

The game is locked to portrait with safe-area-aware touch controls.

On first use, level 1 introduces both control modes. Pass it with buttons, then it automatically restarts with the steering wheel. Failures retry the current mode. After passing with the wheel, choose a preferred mode and continue to level 2. The introduction stage and chosen mode are saved on the device. Until both trials are complete, level selection and control switching are locked.

In wheel mode, hold the wheel to drive and drag around its centre to steer (up to 135 degrees each way). Release to stop immediately and centre the wheel. Tap the gearshift to select forward or reverse; shifting stops the car and requires touching the wheel again. The Controls button in the menu row opens the mode chooser and pauses the game. Switching modes preserves the level and timer, clears input, and stops the car. Focus loss and pauses also release the wheel.

In button mode, hold LEFT / RIGHT with DRIVE / REVERSE; BRAKE stops the car. WASD/arrows, Space, and R remain available on desktop.

Each level starts with a 1.5-second camera animation from a top-down view of the whole course to the car’s isometric driving view. Driving and the timer pause during the intro. Afterward, use ZOOM (left = farther, right = closer). The slider stays visible in levels 1–5. From level 6, a Zoom button shares the Levels/Restart row and toggles the slider; accelerating or starting/restarting a level hides it. Zoom is retained between levels within the session.

The Levels menu pauses play and shows ten levels per page. Use Previous / Next or enter a level number and tap Go. All 500 levels are selectable. Continue advances after success, and level 500 shows campaign completion. The last selected level is remembered on the device.

Repeated `Resources/front` sprites mark an obstacle-aware approach from the spawn to the parking bay in every generated level. Turns use smooth quadratic curves, with tighter rounding where obstacles limit clearance. Arrows remain evenly spaced along the route and follow its changing direction. They point toward the destination even in reverse-parking levels; the arrow inside the bay still specifies the final car heading. Routes are built once per instantiated layout and reused on restart.

Park the entire car in the green bay, face its arrow, and remain stopped for two seconds. Reverse levels require reversing into the bay. Each attempt resets its timer and contact count.

## Editor tools

- **Tools > City Parking > Build 500 Level Scene** regenerates the scene, materials, template prefabs, and catalog. It replaces manual edits to generated assets.
- **Tools > City Parking > Upgrade to 500 Generated Levels** migrates an original ten-root scene into the shared neighborhood and prefab catalog. Already migrated scenes are left intact.
- **Tools > City Parking > Validate All Generated Levels** checks all 500 definitions for determinism, uniqueness, minimum difficulty limits, collision-free spawn/parking footprints, and connected approach corridors.
- **Tools > City Parking > Upgrade Isometric Mobile Camera and UI** updates the saved scene's camera and UI components.

In a disposable project copy, run Unity with `-batchmode -nographics -executeMethod ParkingCatalogValidation.Run -quit` for catalog validation. Run `-batchmode -executeMethod ParkingValidation.Run` (without `-quit`) for Play Mode checks. The latter validates 15 representative parking completions, single-level activation, reverse entry, collision failure, restart, portrait UI, touch input, zoom, pagination, and jump-to-level; it restores the pre-test saved level.

Corridor connectivity is a geometric sanity check, not proof of steering feasibility. Manual driving and physical-device performance testing are still needed for difficulty tuning. Assets use the requested Kenney suburban FBX folder and the included Kenney car kit.

Run `-batchmode -executeMethod ParkingControlsValidation.Run` in a disposable project copy for wheel input, gear changes, immediate stopping, tutorial transitions, saved preferences, and existing mobile UI checks. The test restores saved player preferences.
