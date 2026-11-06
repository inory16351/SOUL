# JOB_ID: 04_traces_landmarks — route traces and passage landmarks

## File you may edit
- `unity/Assets/SOUL/Runtime/World/TraceProps.cs` (only this file)

Read the header comment of `TraceProps.cs` and `World/ZoneRoutes.cs` (placement logic; do not edit).

1. `Trace(zone, seed)` returns `(id, build)`: an id string (unique per model kind, used as cache key)
   and a builder `Func<int, PropModel>` (argument = variant seed). Add new private static builder
   methods in this file. Traces lie ON the trail, no colliders, no surfaces, ≤ 0.6 m high:
   claw scratches in the ground, drag marks, oil trails, shed armour plates, a broken hound husk,
   bones (woods / mire), sludge footprints (mire), cart tracks (foundry). 2–3 kinds per zone, ≤ ~600 tris.
   They tell the player "something went this way" toward the passage.
2. `Landmark(zone)` returns `(id, build)` for the destination zone's silhouette placed just past each
   passage beside the trail. One per zone (list in the header comment). Tall (20–45 m), readable from
   the top-down camera when within ~25 m (interesting top and shadow), footprint radius ≤ 10 m with
   colliders (`PropCollider`) around the base, no walkable surfaces. ≤ ~5,000 tris.
   Set `PropModel.footprint` and `height` correctly; set `landmark = true`.
   Use the same `PropModel` construction pattern as `RegionProps.cs` (`Finish`-style).
