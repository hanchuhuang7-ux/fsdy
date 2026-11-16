import { Group, InstancedMesh, DynamicDrawUsage } from "three";

/** Reuses unit hierarchies for animation, submits shared geometry in GPU batches. */
export class ActorBatches {
  constructor(source, capacity = 512) {
    this.source = source;
    this.capacity = capacity;
    this.group = new Group();
    this.batches = new Map();
    this.meshCount = 0;
    this.batchCount = 0;
    source.visible = false;
  }
  sync() {
    this.meshCount = 0;
    this.batchCount = 0;
    for (const b of this.batches.values()) b.count = 0;
    this.source.updateMatrixWorld(true);
    const visit = (object) => {
      if (!object.visible) return;
      if (object.isMesh) {
        const key = object.geometry.uuid + object.material.uuid;
        let batch = this.batches.get(key);
        if (!batch) {
          batch = new InstancedMesh(
            object.geometry,
            object.material,
            this.capacity,
          );
          batch.instanceMatrix.setUsage(DynamicDrawUsage);
          batch.frustumCulled = false;
          batch.castShadow = object.castShadow;
          batch.receiveShadow = object.receiveShadow;
          batch.count = 0;
          this.group.add(batch);
          this.batches.set(key, batch);
        }
        if (batch.count >= this.capacity)
          throw Error("Instance batch capacity exceeded");
        batch.setMatrixAt(batch.count++, object.matrixWorld);
        this.meshCount++;
      }
      for (const child of object.children) visit(child);
    };
    for (const child of this.source.children) visit(child);
    for (const [key, b] of this.batches) {
      b.visible = b.count > 0;
      if (b.count) {
        b.instanceMatrix.needsUpdate = true;
        this.batchCount++;
      } else if (b.material.userData.transient) {
        this.group.remove(b);
        b.dispose();
        b.material.dispose();
        this.batches.delete(key);
      }
    }
  }
  dispose() {
    for (const b of this.batches.values()) b.dispose();
    this.batches.clear();
    this.group.clear();
  }
}
