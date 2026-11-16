const PRESETS = {
  bow: [480, 180, 0.08, "triangle"],
  crossbow: [170, 65, 0.07, "square"],
  melee: [120, 50, 0.065, "triangle"],
  block: [800, 300, 0.07, "square"],
  slam: [95, 28, 0.21, "sine"],
  charge: [220, 440, 0.28, "sawtooth"],
  catapult: [75, 190, 0.18, "triangle"],
  explosion: [65, 22, 0.3, "sawtooth"],
  heal: [660, 1000, 0.13, "sine"],
  place: [440, 650, 0.055, "sine"],
  start: [180, 540, 0.3, "triangle"],
  order: [330, 660, 0.2, "sine"],
  bomb: [90, 180, 0.1, "triangle"],
};
/** Lazy Web Audio: no network resources, no context until a trusted UI interaction. */
export class BattleAudio {
  constructor() {
    this.context = null;
    this.volume = 0.25;
    this.muted = false;
    this.active = 0;
    this.last = new Map();
    this.cursor = 0;
    this.frame = 0;
    this.frameVoices = 0;
  }
  async unlock() {
    try {
      const C = globalThis.AudioContext || globalThis.webkitAudioContext;
      if (!C) return false;
      if (!this.context) {
        this.context = new C();
        this.master = this.context.createGain();
        this.compressor = this.context.createDynamicsCompressor();
        this.master.connect(this.compressor);
        this.compressor.connect(this.context.destination);
        this.setVolume(this.volume);
      }
      if (this.context.state === "suspended") await this.context.resume();
      return this.context.state === "running";
    } catch {
      return false;
    }
  }
  setVolume(volume) {
    this.volume = Math.max(0, Math.min(1, Number(volume) || 0));
    if (this.master)
      this.master.gain.setTargetAtTime(
        this.muted ? 0 : this.volume * 0.18,
        this.context.currentTime,
        0.02,
      );
  }
  setMuted(muted) {
    this.muted = Boolean(muted);
    this.setVolume(this.volume);
  }
  reset() {
    this.cursor = 0;
    this.last.clear();
  }
  play(type, strength = 1) {
    const ctx = this.context,
      p = PRESETS[type];
    if (!ctx || ctx.state !== "running" || this.muted || !p) return;
    const now = ctx.currentTime,
      frame = Math.floor(now * 60);
    if (frame !== this.frame) {
      this.frame = frame;
      this.frameVoices = 0;
    }
    if (
      this.active >= 12 ||
      this.frameVoices >= 4 ||
      now - (this.last.get(type) ?? -1) < 0.055
    )
      return;
    this.last.set(type, now);
    this.frameVoices++;
    this.active++;
    const o = ctx.createOscillator(),
      g = ctx.createGain();
    o.type = p[3];
    o.frequency.setValueAtTime(p[0], now);
    o.frequency.exponentialRampToValueAtTime(p[1], now + p[2]);
    g.gain.setValueAtTime(0.0001, now);
    g.gain.exponentialRampToValueAtTime(
      Math.max(0.001, Math.min(1, strength)) * 0.6,
      now + 0.007,
    );
    g.gain.exponentialRampToValueAtTime(0.0001, now + p[2]);
    o.connect(g);
    g.connect(this.master);
    o.start(now);
    o.stop(now + p[2] + 0.01);
    o.onended = () => {
      o.disconnect();
      g.disconnect();
      this.active--;
    };
  }
  consume(cues, allow = true) {
    for (const c of cues || [])
      if (c.id > this.cursor) {
        if (allow) this.play(c.type, c.strength);
        this.cursor = c.id;
      }
  }
}
