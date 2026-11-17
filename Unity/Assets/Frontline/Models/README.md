# v0.7 low polygon models

These original texture-free models are generated offline by `Frontline.Editor.ModelBaker.Bake()` (also available from the Frontline editor menu). The build preparation runs the same deterministic baker. Runtime only loads the generated Mesh assets under `Resources/Models`; it never creates primitive GameObjects or constructs soldier meshes.

Every asset has flat, outward-facing normals, per-vertex albedo and a second `_lod1` variant. Vertex RGB is the natural material colour; alpha is the weight for multiplication by the instanced faction colour. Cloth uses a near-white RGB with alpha 1; wood, metal, skin and vegetation use alpha 0. No texture lookup is needed. Baked meshes contain no Collider or physics component.

Unit meshes use the torso centre as their root. `body_<type>` includes the head, helmet, pauldrons and carried back equipment; `arm_right_<type>` includes the weapon; `arm_left` and `shield` animate independently. `leg` is centred at the origin and is placed at `(±0.15,-0.53,0)`. `horse` includes the body, head, ears, mane, saddle and tail; `horse_leg` points downward from its own origin. `catapult` includes its frame and spoked wheels; `catapult_arm` pivots at local zero and is placed at `(0,0.59,0)`. `wreck_horse` and `wreck_catapult` are recognizable collapsed versions.

Props are rooted at ground height. `platform`, `base`, `cliff` and `wall` have normalized unit bounds; `rock` has approximately unit diameter. The hollow `ring` lies in XZ, with radius 1; `dot` has diameter 1. `arrow` and `bolt` point along +Z with length 1. Every grass/flower/tree/reed mesh is rooted at y=0 for vertex wind. Environment placement remains a visual-only concern in Runtime.

The baker checks non-empty geometry, finite vertices, full colour/normal channels and normalized normals before saving, and writes `model-manifest.json` with the exact Resources paths.
